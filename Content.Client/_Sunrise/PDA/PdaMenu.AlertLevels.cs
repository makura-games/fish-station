using Content.Shared.PDA;
using Robust.Shared.Utility;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемому upstream-интерфейсу.
namespace Content.Client.PDA;

public sealed partial class PdaMenu
{
    /*
     * Rendering of primary and additional station alert levels.
     */

    private void UpdateAlertLevelState(PdaIdInfoText state)
    {
        IReadOnlyList<PdaAlertLevelInfo> activeAlertLevels = state.StationAlertLevels is { } stationAlertLevels
            ? stationAlertLevels
            : Array.Empty<PdaAlertLevelInfo>();

        if (activeAlertLevels.Count == 0 && state.StationAlertLevel is { } primaryLevel)
            activeAlertLevels = [new PdaAlertLevelInfo(primaryLevel, state.StationAlertColor)];

        if (activeAlertLevels.Count == 0)
        {
            (_alertLevel, _, _instructions) = _alert.AlertLevelData((string?) null);
            return;
        }

        var localizedAlertLevels = new List<string>(activeAlertLevels.Count);
        var instructions = new List<string>(activeAlertLevels.Count);
        foreach (var activeAlertLevel in activeAlertLevels)
        {
            var (name, _, instruction) = _alert.AlertLevelData(activeAlertLevel.Level);
            var localizedLevel = FormattedMessage.EscapeText(name);
            localizedAlertLevels.Add($"[color={activeAlertLevel.Color.ToHex()}]{localizedLevel}[/color]");
            instructions.Add(instruction);
        }

        _alertLevel = string.Join(", ", localizedAlertLevels);
        _instructions = string.Join("\n", instructions);
    }
}
