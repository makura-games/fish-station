using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Server._Fish.PerformanceMetrics;

/// <summary>Признак, который заметило наблюдение. Сам по себе наказанием не является.</summary>
public enum PmFlag
{
    /// <summary>Путь за тик стабильно выше разрешённого скорости.</summary>
    Motion,

    /// <summary>Попадания с почти нулевой угловой ошибкой сериями.</summary>
    Aim,

    /// <summary>Машинный ритм ударов.</summary>
    Cadence,

    /// <summary>Резкие довороты на цель за один тик.</summary>
    Flick,

    /// <summary>Удары дальше дистанции или сквозь препятствие.</summary>
    Reach,

    /// <summary>Частота действий выше человеческой.</summary>
    Actions,

    /// <summary>Личность не подтверждена: много аккаунтов с того же адреса или со слабого hwid.</summary>
    Identity,
}

/// <summary>Состояние наблюдения за одной сессией.</summary>
public sealed class PmSession
{
    public required NetUserId UserId;

    /// <summary>Признаки, которые уже предъявляли этой сессии, и когда.</summary>
    public readonly Dictionary<PmFlag, TimeSpan> FlaggedAt = new();

    /// <summary>Сколько раз подряд метрика движения вышла за норму.</summary>
    public int MotionStrikes;

    /// <summary>Сколько подряд ударов легли «в центр».</summary>
    public int PerfectHits;

    /// <summary>Сколько снапов через большой угол накопилось.</summary>
    public int FlickStrikes;

    /// <summary>Сколько ударов вне дистанции или сквозь препятствие накопилось.</summary>
    public int ReachStrikes;

    /// <summary>Сколько окон подряд действия шли чаще лимита.</summary>
    public int ActionStrikes;

    /// <summary>Действий в текущем окне.</summary>
    public int ActionsInWindow;

    public TimeSpan WindowEnds;

    /// <summary>Последние межударные интервалы (секунды), для оценки ритма.</summary>
    public readonly List<float> HitGaps = new();

    /// <summary>Куда и под каким углом бил в прошлый раз.</summary>
    public TimeSpan LastHitAt;

    public bool HasLastHit;

    /// <summary>Куда смотрел вектор на цель в прошлый раз и когда.</summary>
    public double? LastAim;

    public TimeSpan LastAimAt;

    /// <summary>Когда в последний раз за это снимали карму.</summary>
    public TimeSpan LastKarmaAt;

    /// <summary>Когда в последний раз докладывали админу.</summary>
    public TimeSpan LastReportAt;

    /// <summary>Сколько всего признаков набралось за сессию.</summary>
    public int TotalFlags;

    public bool HasFlag(PmFlag flag) => FlaggedAt.ContainsKey(flag);

    public void Mark(PmFlag flag, TimeSpan now)
    {
        FlaggedAt[flag] = now;
        TotalFlags++;
    }
}
