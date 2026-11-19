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

    private readonly HashSet<NetUserId> _bannedSessions = new();

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
        if (!_bannedSessions.Add(session.UserId))
            return;

        var karma = suspect.Karma.ToString("0.##", CultureInfo.InvariantCulture);
        var text = _cfg.GetCVar(RpuCVars.BanDuration);

        if (!RpuBanDuration.TryParse(text, out var duration, out var error))
        {
            _sawmill.Error($"Invalid rpu.ban_duration '{text}': {error} Applying permanent ban instead.");
            duration = null;
        }

        var info = new CreateServerBanInfo(BanReason);
        info.AddUser(session.UserId, session.Name);
        info.WithSeverity(NoteSeverity.High);

        if (duration is { } time)
            info.WithMinutes((int)Math.Round(time.TotalMinutes));

        _bans.CreateServerBan(info);

        suspect.Dirty = true;
        _sawmill.Info($"Automatic ban issued for {session.Name}: karma {karma}, " +
                       $"threshold {threshold.ToString("0.##", CultureInfo.InvariantCulture)}, " +
                       $"duration {(duration is null ? "permanent" : duration.Value.ToString())}");

        session.Channel?.Disconnect(BanReason);
    }
}
