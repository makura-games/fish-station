using System.Globalization;
using Content.Shared.Database;
using Robust.Shared.Player;

namespace Content.Server._Fish.Rpu;

/// <summary>
/// Служебные записи системы: начисление/снятие кармы и одиночное уведомление админ-чата.
/// </summary>
public sealed partial class RpuSystem
{
    /// <summary>
    /// Пишем только сам факт изменения кармы. Никаких упоминаний системы и описаний действий.
    /// </summary>
    private void LogKarmaChange(ICommonSession session, float delta, string reason)
    {
        var amount = Math.Abs(delta).ToString("0.##", CultureInfo.InvariantCulture);
        var sign = delta >= 0 ? "+" : "-";
        var character = session.AttachedEntity is { } entity ? ToPrettyString(entity) : "unknown";

        _adminLogs.Add(LogType.Karma, LogImpact.Low,
            $"{session.Name} ({character}) karma {sign}{amount} ({reason})");
    }

    /// <summary>
    /// Одно уведомление админ-чату: когда карма впервые пересекла порог сверху вниз.
    /// Пока игрок не поднимется выше нуля, повторов не будет.
    /// </summary>
    private void CheckChatThreshold(ICommonSession session, RpuSuspect suspect, float before, float penalty)
    {
        var threshold = _cfg.GetCVar(RpuCVars.ChatLogThreshold);

        if (suspect.Karma > 0f)
            suspect.ResetChatLogGate();

        if (suspect.ChatLogSent || before <= threshold || suspect.Karma > threshold)
            return;

        suspect.ChatLogSent = true;

        var character = session.AttachedEntity is { } entity ? ToPrettyString(entity) : "unknown";
        var position = DescribePosition(session.AttachedEntity);

        _chat.SendAdminAnnouncement(
            $"Suspicious activity detected for {session.Name} ({character}) at {position}. Review recommended.");
    }

    private string DescribePosition(EntityUid? uid)
    {
        if (uid == null)
            return "unknown location";

        var map = _transforms.GetMapCoordinates(uid.Value);
        var x = (int)Math.Round(map.Position.X);
        var y = (int)Math.Round(map.Position.Y);

        return $"{x},{y} of map {map.MapId}";
    }
}
