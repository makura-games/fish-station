using System.Numerics;
using System.Runtime.InteropServices;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

using Robust.Shared.Player;
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

    /// <summary>Прежняя выборка: сетка, локальная позиция на ней и момент съёма.</summary>
    private readonly Dictionary<EntityUid, (EntityUid? Grid, Vector2 Position, TimeSpan When)> _previous = new();

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

        // TickRate — целое число тиков в секунду, поэтому везде делим на float: иначе 6/30
        // даёт ноль, и окно выборки исчезает вместе с метрикой.
        var sampleSeconds = PmTuning.MotionSampleTicks / (float)_timing.TickRate;
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

            var allowed = Math.Max(speed.CurrentWalkSpeed, speed.CurrentSprintSpeed);

            // Считаем в локальных координатах сетки: сама станция может ехать (ECS),
            // и в мировых координатах обычная ходьба выглядела бы как телепорт.
            // Смена сетки (посадка в шаттл, выход в космос) — это не перемещение, её пропускаем.
            var grid = xform.GridUid;
            var position = xform.Coordinates.Position;

            if (_previous.TryGetValue(uid, out var last))
            {
                var moved = last.Grid == grid
                    ? (position - last.Position).Length()
                    : 0f;

                // Интервал берём фактический, а не «одно выборочное окно»: сущность могла
                // пропустить съём (мертва, нет компонента скорости), и тогда между выборками
                // прошло больше времени. С фиксальным окном это выглядело бы как превышение.
                var elapsed = Math.Max((now - last.When).TotalSeconds, sampleSeconds);
                var excess = PmMaths.SpeedExcessTiles(moved, allowed, (float)elapsed, PmTuning.SpeedMargin);

                if (excess > PmTuning.MotionMinExcessTiles)
                    CountMotion(actor, excess, moved, allowed, (float)elapsed);
                else if (_pm.TryGetSession(actor.PlayerSession.UserId, out var ok))
                    ok.MotionStrikes = 0;

                // Сменил сетку — накопленные подозрения по предыдущему участку обнуляем.
                if (last.Grid != grid && _pm.TryGetSession(actor.PlayerSession.UserId, out var switched))
                    switched.MotionStrikes = 0;
            }

            _previous[uid] = (grid, position, now);
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
