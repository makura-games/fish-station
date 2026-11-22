namespace Content.Server._Fish.Rpu;

/// <summary>
/// Состояние подозреваемого по сессии: карма, счётчики, служебные флаги.
/// Живёт в памяти сервера, карма при сохранении уходит в БД.
/// </summary>
public sealed class RpuSuspect
{
    /// <summary>Текущая карма от -100 до +100. 0 — игрок впервые зашёл.</summary>
    public float Karma;

    /// <summary>Карма, заработанная наигранным временем (выше неё восстановление не идёт).</summary>
    public float BaseKarma;

    /// <summary>Часы игры на должностях (без лобби и без админ-времени).</summary>
    public double RoleHours;

    /// <summary>Подтверждённые убийства за раунд.</summary>
    public int KillsThisRound;

    /// <summary>Банов за последние N дней (усиливает систему).</summary>
    public int RecentBans;

    public float BanHistoryUnits;

    public float BanThresholdShift;

    /// <summary>Момент последнего срабатывания системы (для восстановления).</summary>
    public TimeSpan LastViolation;

    /// <summary>Порог админ-чата уже пересекался вниз — повторных логов не делаем.</summary>
    public bool ChatLogSent;

    /// <summary>Карму успешно прочитали из БД. До этого с ней ничего не делаем.</summary>
    public bool KarmaLoaded;

    /// <summary>Игрок отключился, но запись кармы ещё не подтверждена.</summary>
    public bool Offline;

    /// <summary>Запись кармы в базе сейчас в полёте.</summary>
    public bool SaveInFlight;

    /// <summary>Раньше этого момента новую попытку записи не делаем (после сбоя БД).</summary>
    public TimeSpan SaveRetryAt;

    /// <summary>Карму нужно сохранить в БД.</summary>
    public bool Dirty;

    /// <summary>Накопленное, но ещё не записанное в лог восстановление кармы.</summary>
    public float RecoveryPending;

    /// <summary>Потери кармы за последнее время, по возрастанию времени.</summary>
    public readonly List<RpuMaths.RpuLoss> Losses = new();

    /// <summary>Записать потерю и выбросить то, что уже не попадает ни в одно окно.</summary>
    public void AddLoss(TimeSpan now, float amount, TimeSpan keep)
    {
        if (amount <= 0f)
            return;

        Losses.Add(new RpuMaths.RpuLoss(now, amount));

        Losses.RemoveAll(loss => now - loss.At > keep);
    }

    /// <summary>Сколько очков потеряно за последние window.</summary>
    public float LossInWindow(TimeSpan now, TimeSpan window)
    {
        return RpuMaths.LossInWindow(Losses, now, window);
    }

    /// <summary>
    /// Карма поднялась выше нуля — порог админ-чата снова открыт, и следующее падение
    /// ниже порога надо увидеть заново.
    /// </summary>
    public void OpenChatLogGateIfCleared()
    {
        if (Karma > 0f)
            ChatLogSent = false;
    }

    public void MarkViolation(TimeSpan now)
    {
        LastViolation = now;
    }
}
