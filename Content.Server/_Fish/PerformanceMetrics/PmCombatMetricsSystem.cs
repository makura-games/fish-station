using System.Numerics;
using System.Runtime.InteropServices;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

using Content.Shared.CCVar;
using Content.Shared.Interaction;
using Robust.Shared.Configuration;
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
    [Dependency] private IConfigurationManager _cfg = default!;

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

        // Выстрел и удар рукой — разные выборки. Признаки наведения считаются только по
        // выстрелам: у ближнего боя сервер фиксирует поворот уже после замаха, и угол
        // там почти всегда нулевой у любого игрока.
        var ranged = IsShot(pm, attacker, now);

        CountRate(pm, now);
        CountCadence(pm, now, ranged);
        CountAngle(pm, attacker, ev.Victim, now, ranged);
        CountRatio(pm);
    }

    /// <summary>
    /// Выстрелом ли был этот урон. По одному источнику отличить выстрел от удара нельзя:
    /// в обоих случаях приходит тело стрелка (ProjectileSystem передаёт component.Shooter).
    /// Надёжный признак — попытка выстрела того же игрока: она регистрируется на
    /// ShotAttemptedEvent раньше, чем урон доходит до цели. Окно шире одной попытки:
    /// дробь и очередь дают несколько уронов с одного нажатия.
    /// </summary>
    private static bool IsShot(PmSession pm, EntityUid attacker, TimeSpan now)
    {
        for (var i = pm.Attempts.Count - 1; i >= 0; i--)
        {
            var attempt = pm.Attempts[i];
            if (attempt.Shooter != attacker)
                continue;

            return (now - attempt.At).TotalSeconds <= PmTuning.ShotMatchSeconds;
        }

        return false;
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

    /// <summary>Постоянство ритма между ударами. Выстрелы и удары руками — порознь.</summary>
    private void CountCadence(PmSession pm, TimeSpan now, bool ranged)
    {
        if (ranged)
        {
            AddGap(pm.HitGaps, pm.HasLastHit, pm.LastHitAt, now);
            pm.LastHitAt = now;
            pm.HasLastHit = true;

            if (!pm.HasFlag(PmFlag.Cadence) && pm.HitGaps.Count >= PmTuning.CadenceSamples)
            {
                var variation = PmMaths.VariationCoefficient(CollectionsMarshal.AsSpan(pm.HitGaps));
                if (variation < PmTuning.CadenceRegularity)
                    _pm.Note(pm, PmFlag.Cadence,
                        $"shot interval variation {variation:0.####} over {pm.HitGaps.Count} shots");
            }

            return;
        }

        AddGap(pm.MeleeGaps, pm.HasLastMelee, pm.LastMeleeAt, now);
        pm.LastMeleeAt = now;
        pm.HasLastMelee = true;

        if (pm.HasFlag(PmFlag.Cadence) || pm.MeleeGaps.Count < PmTuning.CadenceSamples)
            return;

        var melee = PmMaths.VariationCoefficient(CollectionsMarshal.AsSpan(pm.MeleeGaps));
        if (melee < PmTuning.MeleeCadenceRegularity)
            _pm.Note(pm, PmFlag.Cadence,
                $"melee interval variation {melee:0.####} over {pm.MeleeGaps.Count} hits");
    }

    private static void AddGap(List<float> gaps, bool hasPrevious, TimeSpan previous, TimeSpan now)
    {
        if (!hasPrevious)
            return;

        var gap = (float)(now - previous).TotalSeconds;
        if (gap <= 0.01f)
            return;

        gaps.Add(gap);
        if (gaps.Count > 24)
            gaps.RemoveAt(0);
    }

    /// <summary>Угловая ошибка на момент выстрела и величина доворота до следующего.</summary>
    private void CountAngle(PmSession pm, EntityUid attacker, EntityUid victim, TimeSpan now, bool ranged)
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

        if (!ranged)
        {
            // Урон не от выстрела: удар руками, ногами, отражённая волна. Счётчик
            // «идеальных» попаданий обнуляем, иначе серия обычных ударов выглядела бы
            // как наведение. Смотрим только на дистанцию: удар дальше того, что клиент
            // мог отрисовать, возможен лишь при обойдённой проверке радиуса.
            pm.PerfectHits = 0;
            CountUnreachable(pm, distance);
            return;
        }

        PairAttempt(pm, attacker, now, error);
        CountPerception(pm, attacker, victim, error, distance);

        if (error <= PmTuning.AimPerfectDegrees)
            pm.PerfectHits++;
        else
            pm.PerfectHits = 0;

        if (!pm.HasFlag(PmFlag.Aim) && pm.PerfectHits >= PmTuning.AimStrikeCount)
            _pm.Note(pm, PmFlag.Aim,
                $"{pm.PerfectHits} shots in a row within {PmTuning.AimPerfectDegrees} of the target");

        CountFlick(pm, victim, aim, now);
    }

    /// <summary>
    /// Доворот: если между двумя близкими по времени выстрелами вектор на цель меняется
    /// больше чем на прямой угол, рука такого разворота за тик не делает.
    /// Смена цели — не снап: за окно игрок вправе перестрелить на другого. Подозрителен
    /// только разворот на другого быстрее пары тиков, такого мышчьего рывка не бывает.
    /// </summary>
    private void CountFlick(PmSession pm, EntityUid victim, double aim, TimeSpan now)
    {
        var hasPrevious = pm.LastAim.HasValue;
        var previous = pm.LastAim ?? 0.0;
        var previousAt = pm.LastAimAt;
        var previousTarget = pm.LastAimTarget;

        pm.LastAim = aim;
        pm.LastAimAt = now;
        pm.LastAimTarget = victim;

        if (!hasPrevious || pm.HasFlag(PmFlag.Flick))
            return;

        var sinceSeconds = (float)(now - previousAt).TotalSeconds;
        if (sinceSeconds > PmTuning.ActionWindowSeconds)
            return;

        var turn = PmMaths.AngularErrorDegrees(aim, previous);

        if (turn <= PmTuning.FlickDegreesPerTick)
            return;

        if (previousTarget != victim &&
            sinceSeconds > PmTuning.FlickRetargetTicks / (float)_timing.TickRate)
            return;

        pm.FlickStrikes++;
        if (pm.FlickStrikes >= PmTuning.FlickStrikeCount)
            _pm.Note(pm, PmFlag.Flick, $"{pm.FlickStrikes} snaps over {turn:0} degrees inside one action window");
    }

    /// <summary>
    /// Урон от игрока на дистанции, дальше которой клиент цель уже не видит. Выстрела при
    /// этом не было, поэтому это не промах статистики, а обойдённая проверка радиуса.
    /// </summary>
    private void CountUnreachable(PmSession pm, float distance)
    {
        if (pm.HasFlag(PmFlag.Reach) || distance <= PerceiveRange())
            return;

        pm.ReachStrikes++;
        if (pm.ReachStrikes >= PmTuning.ReachStrikeCount)
            _pm.Note(pm, PmFlag.Reach, $"{pm.ReachStrikes} hits at {distance:0.#} tiles with no shot behind them");
    }

    /// <summary>
    /// Расстояние, дальше которого клиент цель не увидит. Берётся из настройки сервера,
    /// а не из кода: видимость зависит от конфигурации, а не от этого метода.
    /// </summary>
    private float PerceiveRange() => Math.Max(PmTuning.MeleeReachMeters, _cfg.GetCVar(FishCVars.PerfmonPerceiveMeters));

    /// <summary>
    /// Закрываем последнюю незакрытую попытку выстрела: так становится видна доля
    /// попаданий и то, насколько точно цель была взята на прицеле.
    /// </summary>
    private void PairAttempt(PmSession pm, EntityUid attacker, TimeSpan now, float error)
    {
        var matchIndex = -1;
        var matches = 0;

        for (var i = pm.Attempts.Count - 1; i >= 0; i--)
        {
            var attempt = pm.Attempts[i];
            if (attempt.Shooter != attacker ||
                (now - attempt.At).TotalSeconds > PmTuning.ShotMatchSeconds)
            {
                continue;
            }

            // Урон не несёт идентификатор снаряда, поэтому точное сопоставление по id
            // невозможно на этом событии. Если в окне больше одной попытки, попадание
            // неоднозначно: не закрываем старый промах и не искажаем долю попаданий.
            // Для дроби/пробития попытка закрывается только первым уроном.
            if (attempt.Realised)
                return;

            matchIndex = i;
            matches++;
            if (matches > 1)
                return;
        }

        if (matches != 1)
            return;

        pm.Attempts[matchIndex] = pm.Attempts[matchIndex] with
        {
            Realised = true,
            ErrorDegrees = error,
        };
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

        // Порог сравнения берём из настройки сервера: что клиент мог увидеть,
        // зависит от конфигурации видимости, а не оттого, как написан этот метод.
        var perceive = PerceiveRange();

        if (distance <= perceive &&
            _interaction.InRangeUnobstructed(
                _transforms.GetMapCoordinates(attacker),
                _transforms.GetMapCoordinates(victim),
                perceive))
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
