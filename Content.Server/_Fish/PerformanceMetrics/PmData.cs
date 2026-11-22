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

    /// <summary>Попадания туда, где цель не могла быть воспринята: препятствие или дистанция.</summary>
    Los,

    /// <summary>Доля попаданий близка к единице при нулевой угловой ошибке.</summary>
    Ratio,

    /// <summary>Личность не подтверждена: много аккаунтов с того же адреса или со слабого hwid.</summary>
    Identity,
}

/// <summary>Одна попытка обращения к оружию.</summary>
public record struct PmAttempt
{
    public TimeSpan At;

    /// <summary>Кто стрелял: нужно, чтобы урон от выстрела не путался с ударом другого игрока.</summary>
    public EntityUid Shooter;

    /// <summary>Попытка привела к урону.</summary>
    public bool Realised;

    /// <summary>Связь с уроном была неоднозначной: не считать ни промахом, ни попаданием.</summary>
    public bool ExcludedFromRatio;

    /// <summary>Угловая ошибка того удара, в градусах; float.NaN — неизвестно.</summary>
    public float ErrorDegrees;
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

    /// <summary>Сколько подряд замеров дали долю попаданий выше нормы.</summary>
    public int RatioStrikes;

    /// <summary>Сколько попаданий пришло сквозь препятствие или с недостижимой дистанции.</summary>
    public int LosStrikes;

    /// <summary>Последние попытки выстрела: когда, реализована ли, и с какой угловой ошибкой.</summary>
    public readonly List<PmAttempt> Attempts = new();

    /// <summary>Действий в текущем окне.</summary>
    public int ActionsInWindow;

    public TimeSpan WindowEnds;

    /// <summary>Последние межударные интервалы выстрелов (секунды), для оценки ритма.</summary>
    public readonly List<float> HitGaps = new();

    /// <summary>То же для ударов в ближнем бою: смешивать выборки нельзя, ритм разный.</summary>
    public readonly List<float> MeleeGaps = new();

    /// <summary>Куда и под каким углом бил в прошлый раз (выстрел).</summary>
    public TimeSpan LastHitAt;

    public bool HasLastHit;

    /// <summary>Когда был предыдущий удар в ближнем бою.</summary>
    public TimeSpan LastMeleeAt;

    public bool HasLastMelee;

    /// <summary>Куда смотрел вектор на цель в прошлый раз и когда.</summary>
    public double? LastAim;

    public TimeSpan LastAimAt;

    /// <summary>По кому целились в прошлый раз: смена цели сама по себе не снап.</summary>
    public EntityUid? LastAimTarget;

    /// <summary>Когда в последний раз за это снимали карму.</summary>
    public TimeSpan LastKarmaAt;

    /// <summary>Когда в последний раз докладывали админу.</summary>
    public TimeSpan LastReportAt;

    /// <summary>Сколько всего признаков набралось за сессию.</summary>
    public int TotalFlags;

    public void Reset()
    {
        FlaggedAt.Clear();
        MotionStrikes = 0;
        PerfectHits = 0;
        FlickStrikes = 0;
        ReachStrikes = 0;
        ActionStrikes = 0;
        RatioStrikes = 0;
        LosStrikes = 0;
        ActionsInWindow = 0;
        HitGaps.Clear();
        MeleeGaps.Clear();
        Attempts.Clear();
        LastAim = null;
        LastAimTarget = null;
        HasLastHit = false;
        HasLastMelee = false;
        TotalFlags = 0;
    }

    public bool HasFlag(PmFlag flag) => FlaggedAt.ContainsKey(flag);

    public void Mark(PmFlag flag, TimeSpan now)
    {
        FlaggedAt[flag] = now;
        TotalFlags++;
    }
}
