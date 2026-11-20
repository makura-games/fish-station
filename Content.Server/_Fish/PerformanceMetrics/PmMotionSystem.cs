using System.Numerics;
using System.Runtime.InteropServices;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Fish.PerformanceMetrics;

/// <summary>
/// Метрики перемещения: сколько тайлов существо проходит за интервал по сравнению
/// с собственной разрешённой скоростью.
/// </summary>
public sealed partial class PmMotionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transforms = default!;
    [Dependency] private PmSystem _pm = default!;

    private EntityQuery<ActorComponent> _actorQuery = default!;
    private EntityQuery<MovementSpeedModifierComponent> _speedQuery = default!;
    private EntityQuery<MobStateComponent> _mobQuery = default!;

    private readonly Dictionary<EntityUid, (MapId Map, Vector2 Position, TimeSpan When)> _previous = new();

    private int _tickCounter;

    private readonly List<EntityUid> _stale = new();

    public override void Initialize()
    {
        base.Initialize();

        _actorQuery = GetEntityQuery<ActorComponent>();
        _speedQuery = GetEntityQuery<MovementSpeedModifierComponent>();
        _mobQuery = GetEntityQuery<MobStateComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_pm.Enabled)
            return;

        if (++_tickCounter < PmTuning.MotionSampleTicks)
            return;

        _tickCounter = 0;

        var seconds = PmTuning.MotionSampleTicks / _timing.TickRate;
        var now = _timing.CurTime;

        var query = AllEntityQuery<TransformComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out var xform, out var actor))
        {
            if (actor.PlayerSession.Status != SessionStatus.InGame)
                continue;

            if (!_mobQuery.TryGetComponent(uid, out var mob) || mob.CurrentState == MobState.Dead)
                continue;

            if (!_speedQuery.TryGetComponent(uid, out var speed))
                continue;

            var map = xform.MapID;
            var position = _transforms.GetWorldPosition(xform);
            var allowed = Math.Max(speed.CurrentWalkSpeed, speed.CurrentSprintSpeed);

            if (_previous.TryGetValue(uid, out var last) && last.Map == map)
            {
                var moved = (position - last.Position).Length();
                var excess = PmMaths.SpeedExcessTiles(moved, allowed, seconds, PmTuning.SpeedMargin);

                if (excess > PmTuning.MotionMinExcessTiles)
                    CountMotion(actor, excess, moved, allowed, seconds);
                else if (_pm.TryGetSession(actor.PlayerSession.UserId, out var ok))
                    ok.MotionStrikes = 0;
            }

            _previous[uid] = (map, position, now);
        }

        // Сущности, которые исчезли из выборки, вычищаем, иначе словарь растёт весь раунд.
        _stale.Clear();
        foreach (var key in _previous.Keys)
        {
            if (!Deleted(key))
                continue;

            _stale.Add(key);
        }

        foreach (var key in _stale)
            _previous.Remove(key);
    }

    private void CountMotion(ActorComponent actor, float excess, float moved, float allowed, float seconds)
    {
        if (!_pm.TryGetSession(actor.PlayerSession.UserId, out var session))
            return;

        session.MotionStrikes++;

        if (session.MotionStrikes < PmTuning.MotionStrikesBeforeFlag || session.HasFlag(PmFlag.Motion))
            return;

        _pm.Note(session, PmFlag.Motion,
            $"moved {moved:0.#} tiles in {seconds:0.00}s, allowed {allowed:0.#} (over by {excess:0.#})");
    }
}
