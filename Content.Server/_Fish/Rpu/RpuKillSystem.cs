using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Shared._Fish.Rpu;

namespace Content.Server._Fish.Rpu;

/// <summary>
/// Учитывает убийства, доведения до крита и «гибель» (распад на органы).
/// Ведёт собственный журнал урона: движок отдаёт источника только в DamageChangedEvent.
/// </summary>
public sealed partial class RpuKillSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RpuSystem _rpu = default!;
    [Dependency] private RpuAdjustSystem _mods = default!;

    /// <summary>Виктор -> (нападавший -> накопленный урон).</summary>
    private readonly Dictionary<EntityUid, Dictionary<EntityUid, FixedPoint2>> _ledger = new();

    /// <summary>За кем уже зарегистрирована смерть, чтобы не считать дважды за один тик.</summary>
    private readonly HashSet<EntityUid> _deathCharged = new();

    private const int LedgerLimit = 400;

    public override void Initialize()
    {
        base.Initialize();

        // DamageDealtEvent звучит раньше, чем урон ляжет на компонент, значит журнал
        // будет заполнен к моменту смены состояния моба.
        SubscribeLocalEvent<DamageableComponent, DamageDealtEvent>(OnDamageDealt);
        SubscribeLocalEvent<MobStateComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<MobStateComponent, BeingGibbedEvent>(OnGibbed);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
    }

    private void OnRoundStarted(RoundStartedEvent args)
    {
        Clear();
    }

    public void Clear()
    {
        _ledger.Clear();
        _deathCharged.Clear();
    }

    private void OnDamageDealt(Entity<DamageableComponent> ent, ref DamageDealtEvent args)
    {
        var dealt = args.Damage.GetTotal();
        if (dealt <= 0)
            return;

        var uid = ent.Owner;

        if (!HasComp<MobStateComponent>(uid))
            return;

        var attacker = ResolveAttacker(args.Origin, uid);
        if (attacker == null)
            return;

        if (_ledger.Count > LedgerLimit && !_ledger.ContainsKey(uid))
            PruneOldest();

        if (!_ledger.TryGetValue(uid, out var perAttacker))
        {
            perAttacker = new Dictionary<EntityUid, FixedPoint2>();
            _ledger[uid] = perAttacker;
        }

        perAttacker.TryGetValue(attacker.Value, out var existing);
        perAttacker[attacker.Value] = existing + dealt;
    }

    private void OnMobStateChanged(EntityUid uid, MobStateComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Critical)
        {
            Charge(uid, args.Origin, RpuPrototypeIds.Crit, crit: true);
            return;
        }

        if (args.NewMobState != MobState.Dead)
            return;

        ChargeDeath(uid, args.Origin);
    }

    private void OnGibbed(EntityUid uid, MobStateComponent component, BeingGibbedEvent args)
    {
        // «Гибать» — это распад на органы: считаем как убийство, но не два раза за одну смерть.
        ChargeDeath(uid, null);
    }

    private void ChargeDeath(EntityUid victim, EntityUid? origin)
    {
        if (!_deathCharged.Add(victim))
            return;

        var violation = IsSecurityChain(victim)
            ? RpuPrototypeIds.KillSecurityChain
            : RpuPrototypeIds.KillCrew;

        Charge(victim, origin, violation, crit: false);
    }

    private void Charge(EntityUid victim, EntityUid? origin, ProtoId<RpuViolationPrototype> violation, bool crit)
    {
        if (!_rpu.Active)
            return;

        if (!_ledger.TryGetValue(victim, out var contributions))
        {
            // Урон шёл без журнала (например, мгновенная смерть): разбираем хотя бы источник.
            if (origin == null)
                return;

            contributions = new Dictionary<EntityUid, FixedPoint2> { [origin.Value] = FixedPoint2.New(1) };
        }

        if (PickKiller(origin, contributions) is not { } killer)
            return;

        if (GetSession(killer) is not { } session)
            return;

        if (!crit)
            _rpu.GetSuspect(session.UserId).KillsThisRound++;

        _rpu.Penalize(session, violation, victim, details: crit ? "crit" : "kill");

        if (!crit)
            _ledger.Remove(victim);
    }

    /// <summary>
    /// Убивший — тот, кто нанёс последний удар, а если удар был окружением, тот, кто набрал больше всего урона.
    /// </summary>
    private EntityUid? PickKiller(EntityUid? origin, Dictionary<EntityUid, FixedPoint2> contributions)
    {
        if (origin != null && GetSession(origin.Value) != null)
            return origin;

        EntityUid? best = null;
        var bestDamage = FixedPoint2.Zero;

        foreach (var (attacker, damage) in contributions)
        {
            if (damage < bestDamage || GetSession(attacker) == null)
                continue;

            best = attacker;
            bestDamage = damage;
        }

        return best;
    }

    private bool IsSecurityChain(EntityUid victim)
    {
        var tuning = _rpu.Tuning;
        _mods.TryGetRolesByEntity(victim, out var roles);

        if (roles.MobPrototype != null && tuning.HighValueMobs.Contains(roles.MobPrototype))
            return true;

        if (roles.Job != null && tuning.SecurityChainJobs.Contains(roles.Job.Value))
            return true;

        return roles.Departments.Any(department => tuning.SecurityChainDepartments.Contains(department));
    }

    private ICommonSession? GetSession(EntityUid uid)
    {
        return CompOrNull<ActorComponent>(uid)?.PlayerSession;
    }

    /// <summary>
    /// Приводит источник урона к сущности игрока: оружие и снаряды указывают на стрелка,
    /// импланты и вживлённые предметы — на носителя.
    /// </summary>
    private EntityUid? ResolveAttacker(EntityUid? origin, EntityUid victim)
    {
        if (origin is not { } candidate)
            return null;

        if (!HasComp<ActorComponent>(candidate))
            return null;

        // Суицид и «сам себе» не считаем нарушением.
        return candidate == victim ? null : candidate;
    }

    private void PruneOldest()
    {
        // Простейшая защита от роста памяти: сбрасываем журнал целиком раз в превышении лимита.
        _ledger.Clear();
        _deathCharged.Clear();
    }
}

