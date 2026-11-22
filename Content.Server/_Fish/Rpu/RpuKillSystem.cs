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

using System.Linq;
using Robust.Shared.Enums;
using Robust.Shared.Audio.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.GameTicking;
using Content.Shared.Gibbing;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Throwing;
using Content.Shared.Wires;
using Content.Shared.Slippery;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;
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

    /// <summary>Жертва -> (игрок -> накопленный урон). Ключ — игрок, а не тело:
    /// тело могут бросить или занять, а отвечать должен тот, кто бил.</summary>
    private readonly Dictionary<EntityUid, Dictionary<NetUserId, FixedPoint2>> _ledger = new();

    /// <summary>За кем уже зарегистрирована смерть, чтобы не считать дважды за один тик.</summary>
    private readonly HashSet<EntityUid> _deathCharged = new();

    private const int LedgerLimit = 400;

    /// <summary>
    /// Существа, за которые когда-либо играл игрок: сюда попадают и те, кто вышел из тела
    /// (призрак, пересадка мозга). Список нужен потому, что по компонентам «тело без игрока»
    /// неотличимо от уборщика или пожарного бота, а карму за ботов снимать нельзя.
    /// </summary>
    private readonly HashSet<EntityUid> _everPlayer = new();

    public override void Initialize()
    {
        base.Initialize();

        // Урон приходит через RpuDamageRouterSystem: направленная подписка на
        // (DamageableComponent, DamageDealtEvent) в движке может быть только одна.
        // Смена состояния моба звучит на всех сущностях, поэтому подписываемся без
        // привязки к компоненту: пара (MobStateComponent, MobStateChangedEvent) занята
        // оглушением, а в самом событии уже лежит и жертва, и источник урона.
        // Смена состояния моба — событие по значению (не ByRefEvent), поэтому обработчик
        // принимает его без ref: иначе движок падает на смешанной подписке.
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<MobStateComponent, BeingGibbedEvent>(OnGibbed);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);

        // Событие звучит на всех сущностях, без привязки к компоненту.
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        _everPlayer.Add(ev.Entity);
    }

    /// <summary>Игровое ли это тело: сейчас под игроком или было под игроком в этом раунде.</summary>
    private bool IsPlayerBody(EntityUid uid)
    {
        return HasComp<ActorComponent>(uid) || _everPlayer.Contains(uid);
    }

    private void OnRoundStarted(RoundStartedEvent args)
    {
        Clear();
    }

    public void Clear()
    {
        _ledger.Clear();
        _deathCharged.Clear();
        _everPlayer.Clear();
    }

    /// <summary>Запоминаем вклад в урон по живому существу. Зовёт маршрутизатор урона.</summary>
    public void RecordDamage(EntityUid uid, EntityUid? origin, FixedPoint2 dealt)
    {
        var attacker = ResolveAttacker(origin, uid);
        if (attacker == null || dealt <= 0)
            return;

        if (!HasComp<MobStateComponent>(uid))
            return;

        // Боты, животные и прочий ИИ не люди: урон по ним не копим вовсе.
        if (!IsPlayerBody(uid))
            return;

        if (_everPlayer.Count > LedgerLimit * 4)
            PruneFormerPlayers();

        if (_ledger.Count > LedgerLimit && !_ledger.ContainsKey(uid))
            PruneOldest();

        if (!_ledger.TryGetValue(uid, out var perAttacker))
        {
            perAttacker = new Dictionary<NetUserId, FixedPoint2>();
            _ledger[uid] = perAttacker;
        }

        perAttacker.TryGetValue(attacker.Value, out var existing);
        perAttacker[attacker.Value] = existing + dealt;
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        var uid = args.Target;

        switch (args.NewMobState)
        {
            case MobState.Alive:
                // Существо ожили: снимаем отметку, иначе следующее убийство не учтём.
                _deathCharged.Remove(uid);
                return;
            case MobState.Critical:
                Charge(uid, args.Origin, RpuPrototypeIds.Crit, crit: true);
                return;
            case MobState.Dead:
                ChargeDeath(uid, args.Origin);
                return;
            default:
                return;
        }
    }

    private void OnGibbed(Entity<MobStateComponent> ent, ref BeingGibbedEvent args)
    {
        // «Гибать» — это распад на органы: считаем как убийство, но не два раза за одну смерть.
        ChargeDeath(ent.Owner, null);
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

        // Уборщик, медбот, пожарный, мышь — не экипаж.
        if (!IsPlayerBody(victim))
            return;

        if (!_ledger.TryGetValue(victim, out var contributions))
        {
            // Урон шёл без журнала (например, мгновенная смерть): разбираем хотя бы источник.
            // Самоубийство нарушением не считается ни на каком пути.
            if (origin == null || origin == victim)
                return;

            if (ResolveAttacker(origin, victim) is not { } direct)
                return;

            contributions = new Dictionary<NetUserId, FixedPoint2> { [direct] = FixedPoint2.New(1) };
        }

        if (PickKiller(victim, origin, contributions) is not { } killer)
            return;

        // Отвечает тот, кто бил, даже если к моменту смерти он уже вышел из тела.
        if (!_rpu.TryGetSession(killer, out var session))
            return;

        if (!crit)
            _rpu.GetSuspect(session.UserId).KillsThisRound++;

        _rpu.Penalize(session, violation, victim, details: crit ? "crit" : "kill");

        if (!crit)
            _ledger.Remove(victim);
    }

    /// <summary>
    /// Убивший — игрок с наибольшим вкладом, а если журнал пуст, источник последнего удара.
    /// Самоубийство отсекается до разбора журнала: иначе чужой старый вклад был бы списан
    /// на того, кто покончил с собой.
    /// </summary>
    private NetUserId? PickKiller(EntityUid victim, EntityUid? origin, Dictionary<NetUserId, FixedPoint2> contributions)
    {
        if (origin != null && origin == victim)
            return null;

        if (ResolveAttacker(origin, victim) is { } self)
            return self;

        NetUserId? best = null;
        var bestDamage = FixedPoint2.Zero;

        foreach (var (attacker, damage) in contributions)
        {
            if (damage < bestDamage)
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

    /// <summary>
    /// Кто бил: игрок, который играет на источнике урона. Оружие и снаряды указывают
    /// на стрелка, импланты и вживлённые предметы — на носителя.
    /// </summary>
    private NetUserId? ResolveAttacker(EntityUid? origin, EntityUid victim)
    {
        if (origin is not { } candidate)
            return null;

        // Суицид и «сам себе» не считаем нарушением.
        if (candidate == victim)
            return null;

        return CompOrNull<ActorComponent>(candidate)?.PlayerSession.UserId;
    }

    private void PruneFormerPlayers()
    {
        _everPlayer.RemoveWhere(uid => Deleted(uid));
    }

    private void PruneOldest()
    {
        // Простейшая защита от роста памяти: сбрасываем журнал целиком раз в превышении лимита.
        _ledger.Clear();
    }
}

