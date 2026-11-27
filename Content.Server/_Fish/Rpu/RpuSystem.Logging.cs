using System.Globalization;
using Content.Shared.Database;
using Robust.Shared.Audio;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Content.Shared.CCVar;

using System.Linq;
using Content.Shared._Fish.Rpu;
using Robust.Shared.Audio.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Gibbing;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Throwing;
using Content.Shared.Wires;
using Content.Shared.Slippery;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;
namespace Content.Server._Fish.Rpu;

/// <summary>
/// Служебные записи системы: начисление/снятие кармы и одиночное уведомление админ-чата.
/// </summary>
public sealed partial class RpuSystem
{
    /// <summary>
    /// Пишем только сам факт изменения кармы. Никаких упоминаний системы и описаний действий.
    /// Игрока и сущности подставляем в строку как объекты, а не как готовые имена:
    /// иначе запись не попадёт в логи игрока и сущности, их ищут по этим ссылкам.
    /// </summary>
    private void LogKarmaChange(ICommonSession session, float delta, string reason, EntityUid? other = null)
    {
        var amount = Math.Abs(delta).ToString("0.##", CultureInfo.InvariantCulture);
        var sign = delta >= 0 ? "+" : "-";

        if (session.AttachedEntity is { } entity && other is { } victim && victim != EntityUid.Invalid)
        {
            _adminLogs.Add(LogType.Karma, LogImpact.Low,
                $"{session} ({entity}) karma {sign}{amount} ({reason}) affecting {victim}");
            return;
        }

        if (session.AttachedEntity is { } alone)
        {
            _adminLogs.Add(LogType.Karma, LogImpact.Low,
                $"{session} ({alone}) karma {sign}{amount} ({reason})");
            return;
        }

        _adminLogs.Add(LogType.Karma, LogImpact.Low,
            $"{session} karma {sign}{amount} ({reason})");
    }

    /// <summary>
    /// Уведомление админам: строка в админ-чат и короткий сигнал тем, кто сейчас
    /// в статусе админа. Игроки этого не видят вовсе.
    /// </summary>
    private void NotifyAdmins(string message)
    {
        _chat.SendAdminAnnouncement(message);

        var admins = _admins.ActiveAdmins.ToArray();
        if (admins.Length == 0)
            return;

        _audio.PlayGlobal("/Audio/Effects/alert.ogg",
            Filter.Empty().AddPlayers(admins), false,
            AudioParams.Default.AddVolume(-6f));
    }

    /// <summary>
    /// Одно уведомление админ-чату: когда карма впервые пересекла порог сверху вниз.
    /// Флаг открывает обратно любое поднятие кармы выше нуля (награда, восстановление,
    /// ручная правка) — см. RpuSuspect.OpenChatLogGateIfCleared.
    /// </summary>
    private void CheckChatThreshold(ICommonSession session, RpuSuspect suspect, float before)
    {
        var threshold = _cfg.GetCVar(FishCVars.RpuChatLogThreshold);

        if (suspect.ChatLogSent || before <= threshold || suspect.Karma > threshold)
            return;

        suspect.ChatLogSent = true;

        var karma = suspect.Karma.ToString("0.##", CultureInfo.InvariantCulture);
        var position = DescribePosition(session.AttachedEntity);

        NotifyAdmins($"Activity level for {session.Name} crossed the review threshold " +
                     $"(karma {karma}, limit {threshold:0.##}) at {position}. Review recommended.");
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
