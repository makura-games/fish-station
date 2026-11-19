namespace Content.Server._Fish.Rpu;

/// <summary>
/// Состояние подозреваемого по сессии: карма, счётчики, служебные флаги.
/// Живёт в памяти сервера, карма при сохранении уходит в БД.
/// </summary>
public sealed class RpuSuspect
{
    /// <summary>Текущая карма от -100 до +100. 0 — игрок впервые зашёл.</summary>
    public float Karma;

    /// <summary>Карма, заработанная наигранным временем (ниже неё восстановление не идёт).</summary>
    public float BaseKarma;

    /// <summary>Часы игры на должностях (без лобби и без админ-времени).</summary>
    public double RoleHours;

    /// <summary>Подтверждённые убийства за раунд.</summary>
    public int KillsThisRound;

    /// <summary>Банов за последние N дней (усиливает систему).</summary>
    public int RecentBans;

    public float BanHistoryMultiplier = 1f;

    public float BanThresholdShift;

    /// <summary>Накопленное время без срабатываний системы, в часах раунда.</summary>
    public float CleanHoursAccumulated;

    /// <summary>Момент последнего срабатывания системы (для восстановления).</summary>
    public TimeSpan LastViolation;

    /// <summary>Порог админ-чата уже пересекался вниз — повторных логов не делаем.</summary>
    public bool ChatLogSent;

    /// <summary>Карму нужно сохранить в БД.</summary>
    public bool Dirty;

    /// <summary>Заметил ли за этот час система (для восстановления).</summary>
    public bool TriggeredThisHour;

    /// <summary>Накопленное, но ещё не записанное в лог восстановление кармы.</summary>
    public float RecoveryPending;

    /// <summary>Карма поднялась выше нуля — можно снова логировать пересечение порога.</summary>
    public void ResetChatLogGate()
    {
        ChatLogSent = false;
    }

    public void MarkViolation(TimeSpan now)
    {
        LastViolation = now;
        CleanHoursAccumulated = 0f;
        TriggeredThisHour = true;
    }
}
