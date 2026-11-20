using Content.Shared.AlertLevel;
using Content.Shared.PDA;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемой upstream-системе.
namespace Content.Server.PDA;

public sealed partial class PdaSystem
{
    /*
     * Synchronization of primary and additional station alert levels to PDAs.
     */

    [Dependency] private AlertLevelSystem _alertLevel = default!;

    private void OnAdditionalAlertLevelChanged(ref AdditionalAlertLevelChangedEvent args)
    {
        UpdateAllPdaUisOnStation();
    }

    private void UpdateAdditionalAlertLevels(
        Entity<PdaComponent> pda,
        Entity<AlertLevelComponent> station)
    {
        var activeLevels = _alertLevel.GetActiveLevels(station.AsNullable());
        var stationAlertLevels = new List<PdaAlertLevelInfo>(activeLevels.Count);
        foreach (var activeLevel in activeLevels)
        {
            if (_prototype.Resolve(activeLevel, out var detail))
                stationAlertLevels.Add(new PdaAlertLevelInfo(activeLevel, detail.Color));
        }

        pda.Comp.StationAlertLevels = stationAlertLevels;
    }
}
