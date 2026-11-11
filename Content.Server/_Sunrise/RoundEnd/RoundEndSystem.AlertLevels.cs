using Content.Shared.AlertLevel;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемой upstream-системе.
namespace Content.Server.RoundEnd;

public sealed partial class RoundEndSystem
{
    /*
     * Emergency-shuttle timing derived from all active alert levels.
     */

    private TimeSpan GetAlertLevelShuttleTime(
        Entity<AlertLevelComponent> station,
        TimeSpan fallback)
    {
        var duration = ProtoMan.Resolve(station.Comp.CurrentAlertLevel, out var currentDetail)
            ? currentDetail.ShuttleTime
            : fallback;

        foreach (var additionalLevel in station.Comp.ActiveAdditionalLevels)
        {
            if (ProtoMan.Resolve(additionalLevel, out var detail)
                && detail.ShuttleTime > duration)
            {
                duration = detail.ShuttleTime;
            }
        }

        return duration;
    }
}
