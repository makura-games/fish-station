using System.Numerics;
using System.Runtime.InteropServices;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Fish.PerformanceMetrics;

/// <summary>
/// Метрики боя: точность попадания, постоянство ритма, величину доворота между ударами
/// и частоту обращений. Считаются только по тем данным, которые сервер и так получает,
/// когда разрешает урон.
/// </summary>
public sealed partial class PmCombatMetricsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transforms = default!;
    [Dependency] private PmSystem _pm = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;

    private EntityQuery<ActorComponent> _actorQuery = default!;
    private EntityQuery<TransformComponent> _xformQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        _actorQuery = GetEntityQuery<ActorComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<PmDamageObservedEvent>(OnDamage);
    }

    private void OnDamage(PmDamageObservedEvent ev)
    {
        if (!_pm.Enabled || ev.Total <= 0f)
            return;

        if (!TryGetAttacker(ev.Source, out var attacker, out var session))
            return;

        var pm = _pm.GetSession(session.UserId);
        var now = _timing.CurTime;

        CountRate(pm, now);
        CountCadence(pm, now);
        CountAngle(pm, attacker, ev.Victim, now);
        CountRatio(pm);
    }

    /// <summary>Частота обращений: сколько ударов прошло за окно.</summary>
    private void CountRate(PmSession pm, TimeSpan now)
    {
        if (now >= pm.WindowEnds)
        {
            var rate = PmMaths.ActionsPerSecond(pm.ActionsInWindow, PmTuning.ActionWindowSeconds);

            if (rate > PmTuning.ActionRateLimit)
                pm.ActionStrikes++;
            else
                pm.ActionStrikes = 0;

            pm.ActionsInWindow = 0;
            pm.WindowEnds = now + TimeSpan.FromSeconds(PmTuning.ActionWindowSeconds);

            if (!pm.HasFlag(PmFlag.Actions) && pm.ActionStrikes >= PmTuning.ActionStrikeCount)
                _pm.Note(pm, PmFlag.Actions, $"{rate:0.#} requests per second sustained over {pm.ActionStrikes} windows");
        }

        pm.ActionsInWindow++;
    }

    /// <summary>Постоянство ритма между ударами.</summary>
    private void CountCadence(PmSession pm, TimeSpan now)
    {
        if (pm.HasLastHit)
        {
            var gap = (float)(now - pm.LastHitAt).TotalSeconds;
            if (gap > 0.01f)
            {
                pm.HitGaps.Add(gap);
                if (pm.HitGaps.Count > 24)
                    pm.HitGaps.RemoveAt(0);
            }
        }

        pm.LastHitAt = now;
        pm.HasLastHit = true;

        if (pm.HasFlag(PmFlag.Cadence) || pm.HitGaps.Count < 10)
            return;

        var variation = PmMaths.VariationCoefficient(CollectionsMarshal.AsSpan(pm.HitGaps));
        if (variation < PmTuning.CadenceRegularity)
            _pm.Note(pm, PmFlag.Cadence, $"hit interval variation {variation:0.####} over {pm.HitGaps.Count} hits");
    }

    /// <summary>Угловая ошибка на момент удара и величина доворота до следующего.</summary>
    private void CountAngle(PmSession pm, EntityUid attacker, EntityUid victim, TimeSpan now)
    {
        if (!_xformQuery.TryGetComponent(attacker, out var attackerXform) ||
            !_xformQuery.TryGetComponent(victim, out var victimXform))
        {
            return;
        }

        if (attackerXform.MapID != victimXform.MapID)
            return;

        var from = _transforms.GetWorldPosition(attackerXform);
        var to = _transforms.GetWorldPosition(victimXform);
        var delta = to - from;
        var distance = delta.Length();

        if (distance < 0.05f)
            return;

        var aim = Math.Atan2(delta.Y, delta.X);
        var error = PmMaths.AngularErrorDegrees(attackerXform.WorldRotation.Theta, aim);

        PairAttempt(pm, now, error);
        CountPerception(pm, attacker, victim, error, distance);

        if (error <= PmTuning.AimPerfectDegrees)
            pm.PerfectHits++;
        else
            pm.PerfectHits = 0;

        if (!pm.HasFlag(PmFlag.Aim) && pm.PerfectHits >= PmTuning.AimStrikeCount)
            _pm.Note(pm, PmFlag.Aim,
                $"{pm.PerfectHits} hits in a row within {PmTuning.AimPerfectDegrees} of the target");

        CountFlick(pm, aim, now);
        CountReach(pm, error, distance);
    }

    /// <summary>
    /// Доворот: если между двумя близкими по времени ударами вектор на цель меняется
    /// больше чем на прямой угол, рука такого разворота за тик не делает.
    /// </summary>
    private void CountFlick(PmSession pm, double aim, TimeSpan now)
    {
        var hasPrevious = pm.LastAim.HasValue;
        var previous = pm.LastAim ?? 0.0;
        var previousAt = pm.LastAimAt;

        pm.LastAim = aim;
        pm.LastAimAt = now;

        if (!hasPrevious || pm.HasFlag(PmFlag.Flick))
            return;

        var sinceSeconds = (float)(now - previousAt).TotalSeconds;
        if (sinceSeconds > PmTuning.ActionWindowSeconds)
            return;

        var turn = Math.Abs(((aim - previous + Math.PI) % (Math.PI * 2.0)) - Math.PI) * (180.0 / Math.PI);

        if (turn <= PmTuning.FlickDegreesPerTick)
            return;

        pm.FlickStrikes++;
        if (pm.FlickStrikes >= PmTuning.FlickStrikeCount)
            _pm.Note(pm, PmFlag.Flick, $"{pm.FlickStrikes} snaps over {turn:0} degrees inside one action window");
    }

    /// <summary>
    /// Попадания «идеально в центр» на дистанции, которой у ближнего боя не бывает:
    /// либо это стрельба с точностью, недоступной вручную, либо удар по тому, чего не видно.
    /// </summary>
    private void CountReach(PmSession pm, float error, float distance)
    {
        if (pm.HasFlag(PmFlag.Reach) || error > PmTuning.AimPerfectDegrees)
            return;

        if (distance <= PmTuning.MeleeReachMeters)
            return;

        pm.ReachStrikes++;
        if (pm.ReachStrikes >= PmTuning.ReachStrikeCount)
            _pm.Note(pm, PmFlag.Reach, $"{pm.ReachStrikes} perfect-angle hits at {distance:0.#} tiles");
    }

    /// <summary>
    /// Закрываем последнюю незакрытую попытку выстрела: так становится видна доля
    /// попаданий и то, насколько точно цель была взята на прицеле.
    /// </summary>
    private void PairAttempt(PmSession pm, TimeSpan now, float error)
    {
        for (var i = pm.Attempts.Count - 1; i >= 0; i--)
        {
            var attempt = pm.Attempts[i];
            if (attempt.Realised)
                continue;

            // Стрелок мог выстрелить раньше, а урон дошёл позже: дальше секунды не связываем.
            if ((now - attempt.At).TotalSeconds > 1.5f)
                return;

            pm.Attempts[i] = attempt with { Realised = true, ErrorDegrees = error };
            return;
        }

        // Попадания без попытки выстрела (ближний бой, зона, отражённый урон) не считаем.
    }

    /// <summary>
    /// Удар по тому, что не могло быть воспринято: дальше дистанции видимого контакта
    /// или сквозь препятствие. Такой признак появляется только когда игрок действительно
    /// бьёт туда, где по данным сервера цели видеть нельзя.
    /// </summary>
    private void CountPerception(PmSession pm, EntityUid attacker, EntityUid victim, float error, float distance)
    {
        if (pm.HasFlag(PmFlag.Los) || error > PmTuning.AimPerfectDegrees)
            return;

        if (distance <= PmTuning.MeleeReachMeters)
            return;

        if (distance <= PmTuning.PerceiveMeters &&
            _interaction.InRangeUnobstructed(
                _transforms.GetMapCoordinates(attacker),
                _transforms.GetMapCoordinates(victim),
                PmTuning.PerceiveMeters))
        {
            return;
        }

        pm.LosStrikes++;

        if (pm.LosStrikes >= PmTuning.LosStrikeCount)
            _pm.Note(pm, PmFlag.Los,
                $"{pm.LosStrikes} perfect hits at {distance:0.#} tiles with no clear line");
    }

    /// <summary>Доля попыток, дошедших до цели, при том что каждая — точно в центр.</summary>
    private void CountRatio(PmSession pm)
    {
        if (pm.HasFlag(PmFlag.Ratio) ||
            pm.Attempts.Count < PmTuning.RatioSampleCount)
        {
            return;
        }

        var realised = PmMaths.RealisedShare(pm.Attempts, 30, PmTuning.RatioSampleCount);
        if (realised < PmTuning.RatioMinimum)
        {
            pm.RatioStrikes = 0;
            return;
        }

        var perfect = PmMaths.PerfectShareOfRealised(pm.Attempts, PmTuning.RatioSampleCount, PmTuning.AimPerfectDegrees);
        if (perfect < 0.9f)
        {
            pm.RatioStrikes = 0;
            return;
        }

        pm.RatioStrikes++;

        if (pm.RatioStrikes >= PmTuning.RatioStrikeCount)
            _pm.Note(pm, PmFlag.Ratio,
                $"{PmTuning.RatioSampleCount} shots: {realised:P0} landed, {perfect:P0} of them dead-centre");
    }

    /// <summary>
    /// Кто нанёс урон: координаты источника могут принадлежать предмету в руках,
    /// тогда берём носителя.
    /// </summary>
    private bool TryGetAttacker(EntityUid? source, out EntityUid attacker, out ICommonSession session)
    {
        attacker = EntityUid.Invalid;
        session = default!;

        // Урон «от карты», от аномалии или от зоны не имеет конкретного носителя.
        if (source == null)
            return false;

        var from = source.Value;
        if (from == EntityUid.Invalid || HasComp<MapComponent>(from))
            return false;

        if (!_xformQuery.TryGetComponent(from, out var sourceXform))
            return false;

        if (TryActor(from, out var direct))
        {
            attacker = from;
            session = direct;
            return true;
        }

        var parent = sourceXform.ParentUid;
        if (parent != EntityUid.Invalid && TryActor(parent, out var holder))
        {
            attacker = parent;
            session = holder;
            return true;
        }

        return false;
    }

    private bool TryActor(EntityUid uid, out ICommonSession session)
    {
        session = default!;

        if (!_actorQuery.TryGetComponent(uid, out var actor))
            return false;

        session = actor.PlayerSession;
        return session.Status == SessionStatus.InGame;
    }
}
