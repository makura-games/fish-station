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

using System.Linq;
using Robust.Shared.Enums;
using Robust.Shared.Audio.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
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

        // Урон приходит через RpuDamageRouterSystem: направленная подписка на эту пару
        // компонента и события в движке может быть только одна.
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

    /// <summary>
    /// Запоминаем, кто бил конструкцию. Зовёт маршрутизатор урона.
    /// </summary>
    public void RecordDamage(EntityUid uid, EntityUid? origin, FixedPoint2 total)
    {
        if (!_rpu.Active || total <= 0)
            return;

        if (!IsStructure(uid))
            return;

        if (origin is not { } attacker || CompOrNull<ActorComponent>(attacker) == null)
            return;

        _lastDamager[uid] = (attacker, _timing.CurTime);
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

        // Пока счёт не ведётся (эвакуация, режим, отключение), накопленные возмездия
        // не выставляем: они были заработаны в другом состоянии счёта.
        if (!_rpu.Active)
        {
            _lastDamager.Clear();
            return;
        }

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
        List<EntityUid>? expired = null;

        foreach (var (uid, entry) in _lastDamager)
        {
            if (entry.Time >= cutoff)
                continue;

            expired ??= new List<EntityUid>();
            expired.Add(uid);
        }

        if (expired == null)
            return;

        foreach (var uid in expired)
            _lastDamager.Remove(uid);
    }
}
