using System.Globalization;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Shared.Player;

namespace Content.Server._Fish.Rpu;

/// <summary>
/// Автобан по карме. Порог зависит от наигранного времени и от количества последних банов.
/// </summary>
public sealed partial class RpuSystem
{
    public const string BanReason =
        "Забанен автоматической системой. Если произошла ошибка, обратитесь за помощью на наш дискорд сервер.";

    /// <summary>
    /// Сессии, которым уже дали бан за этот заход. Держим сами объекты сессий: после
    /// переподключения это уже другая сессия, и порог должен сработать снова.
    /// </summary>
    private readonly HashSet<ICommonSession> _bannedSessions = new();

    /// <summary>
    /// Последний разобранный срок. Кривой конфиг не должен превращать временный бан
    /// в бессрочный: держим прошее допустимое значение, а если допустимого не было —
    /// наказание не выдаём вовсе.
    /// </summary>
    private TimeSpan? _lastGoodDuration;
    private bool _durationKnownGood;

    private void CheckAutoBan(ICommonSession session, RpuSuspect suspect)
    {
        if (!PunishmentAllowed(session))
            return;

        var threshold = RpuMaths.BanThreshold(
            suspect.RoleHours,
            Tuning.BanTiers,
            suspect.BanThresholdShift,
            _cfg.GetCVar(RpuCVars.BanVeterans));

        if (float.IsNegativeInfinity(threshold) || suspect.Karma > threshold)
            return;

        ApplyAutoBan(session, suspect, threshold);
    }

    private void ApplyAutoBan(ICommonSession session, RpuSuspect suspect, float threshold)
    {
        // Одна попытка бана на сессию, чтобы не плодить записи при повторяющихся срабатываниях.
        if (!_bannedSessions.Add(session))
            return;

        if (!TryDuration(out var duration))
        {
            // Значение срока разобрать не удалось — ничего не выдаём, право повторить
            // остаётся при следующем нарушении.
            _bannedSessions.Remove(session);
            return;
        }

        var karma = suspect.Karma.ToString("0.##", CultureInfo.InvariantCulture);

        var info = new CreateServerBanInfo(BanReason);
        info.AddUser(session.UserId, session.Name);
        info.WithSeverity(NoteSeverity.High);

        // null — это осознанный «навсегда»: rpu.ban_duration = 0. Положительный срок
        // передаем целиком, округление до минут не превращает доли минуты в отказ.
        if (duration is { } time)
            info.WithDuration(time);

        // Запись в базу и отключение игрока делает бан-менеджер — уже после сохранения.
        // Наш disconnect оторвал бы игрока от ещё не сохранённого бана.
        _bans.CreateServerBan(info);

        suspect.Dirty = true;
        _sawmill.Info($"Automatic ban issued for {session.Name}: karma {karma}, " +
                       $"threshold {threshold.ToString("0.##", CultureInfo.InvariantCulture)}, " +
                       $"duration {(duration is null ? "permanent" : duration.Value.ToString())}");
    }

    /// <summary>
    /// Разбирает срок из конфига: true + null означает «навсегда», false — «не выдаём».
    /// </summary>
    private bool TryDuration(out TimeSpan? duration)
    {
        var text = _cfg.GetCVar(RpuCVars.BanDuration);

        if (RpuBanDuration.TryParse(text, out var parsed, out _))
        {
            _lastGoodDuration = parsed;
            _durationKnownGood = true;
            duration = parsed;
            return true;
        }

        var advice = _durationKnownGood
            ? "keeping the last valid value"
            : "no valid value was ever configured, issuing is suspended";

        _sawmill.Error($"Invalid rpu.ban_duration '{text}': {advice}.");

        duration = _lastGoodDuration;
        return _durationKnownGood;
    }

    /// <summary>Отключился игрок — снимаем отметку, чтобы следующая сессия считалась заново.</summary>
    private void ForgetBannedSession(ICommonSession session)
    {
        _bannedSessions.Remove(session);
    }
}
