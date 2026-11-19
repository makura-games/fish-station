using Content.Shared._Fish.Rpu;
using Content.Shared.Construction.Components;
using Content.Shared.Damage;
using Content.Shared.Doors.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Content.Shared.Wall;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Rpu;

/// <summary>
/// Разрушение и разбор стен, окон и шлюзов. Бумажки, ящики и станки не считаем —
/// чтобы система не наказывала за обычную уборку.
/// </summary>
public sealed partial class RpuDestructionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RpuSystem _rpu = default!;
    [Dependency] private TagSystem _tags = default!;

    /// <summary>Кто повредил конструкцию и когда.</summary>
    private readonly Dictionary<EntityUid, (EntityUid Attacker, TimeSpan Time)> _lastDamager = new();

    private float _updateAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageableComponent, DamageDealtEvent>(OnDamageDealt);
        SubscribeLocalEvent<WallComponent, BeforeUnanchoredEvent>(OnUnanchored);
        SubscribeLocalEvent<AirlockComponent, BeforeUnanchoredEvent>(OnUnanchored);
    }

    private static readonly ProtoId<TagPrototype> WindowTag = "Window";

    private bool IsStructure(EntityUid uid)
    {
        return HasComp<WallComponent>(uid)
               || HasComp<AirlockComponent>(uid)
               || HasComp<DoorBoltComponent>(uid)
               || _tags.HasTag(uid, WindowTag);
    }

    private void OnDamageDealt(Entity<DamageableComponent> ent, ref DamageDealtEvent args)
    {
        if (!_rpu.Active || args.Damage.GetTotal() <= 0)
            return;

        if (!IsStructure(ent.Owner))
            return;

        if (args.Origin is not { } origin || CompOrNull<ActorComponent>(origin) == null)
            return;

        _lastDamager[ent.Owner] = (origin, _timing.CurTime);
        Trim();
    }

    /// <summary>
    /// Проверяем раз в секунду только список повреждённых конструкций — никого не опрашиваем широко.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateAccumulator += frameTime;
        if (_updateAccumulator < 1f)
            return;

        _updateAccumulator = 0f;

        if (_lastDamager.Count == 0)
            return;

        foreach (var (uid, entry) in _lastDamager.ToArray())
        {
            // Конструкция исчезла — значит её разбили.
            var destroyed = !Exists(uid)
                            || EntityManager.IsQueuedForDeletion(uid)
                            || TerminatingOrDeleted(uid);

            if (!destroyed)
            {
                if (_timing.CurTime - entry.Time > TimeSpan.FromSeconds(20))
                    _lastDamager.Remove(uid);

                continue;
            }

            _lastDamager.Remove(uid);

            if (CompOrNull<ActorComponent>(entry.Attacker)?.PlayerSession is not { } session)
                continue;

            _rpu.Penalize(session, RpuPrototypeIds.StructureDestroyed, details: "structure destroyed");
        }
    }

    private bool TerminatingOrDeleted(EntityUid uid)
    {
        return MetaData(uid).EntityLifeStage is EntityLifeStage.Terminating or EntityLifeStage.Deleted;
    }

    private void OnUnanchored(Entity<WallComponent> ent, ref BeforeUnanchoredEvent args)
    {
        ChargeDeconstruct(ent.Owner, args.User);
    }

    private void OnUnanchored(Entity<AirlockComponent> ent, ref BeforeUnanchoredEvent args)
    {
        ChargeDeconstruct(ent.Owner, args.User);
    }

    private void ChargeDeconstruct(EntityUid uid, EntityUid user)
    {
        if (!_rpu.Active)
            return;

        if (CompOrNull<ActorComponent>(user)?.PlayerSession is not { } session)
            return;

        // Разбор конструкции (открутили опоры) — меньший штраф, но он есть.
        _rpu.Penalize(session, RpuPrototypeIds.StructureDeconstructed, details: "structure deconstructed");
    }

    private void Trim()
    {
        if (_lastDamager.Count < 512)
            return;

        var cutoff = _timing.CurTime - TimeSpan.FromSeconds(30);
        foreach (var (uid, entry) in _lastDamager)
        {
            if (entry.Time < cutoff)
                _lastDamager.Remove(uid);
        }
    }
}
