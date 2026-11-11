// Fish-start: импорты для AlertLevelPrototype
using Content.Shared.AlertLevel;
using Robust.Shared.Prototypes;
// Fish-end
using Content.Shared.Communications;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемому upstream-интерфейсу.
namespace Content.Client.Communications.UI;

public sealed partial class CommunicationsConsoleMenu
{
    /*
     * Controls for additional alert levels and remote station selection.
     */

    // Fish-start: внедрение зависимостей локализации и прототипов
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;
    // Fish-end

    /// <summary>
    /// Raised when an additional alert level is toggled.
    /// </summary>
    // Fish-start: типизированный ProtoId<AlertLevelPrototype> вместо string
    // public event Action<string, bool>? OnAdditionalAlertLevel;
    public event Action<ProtoId<AlertLevelPrototype>, bool>? OnAdditionalAlertLevel;
    // Fish-end

    /// <summary>
    /// Raised when another target station is selected.
    /// </summary>
    public event Action<NetEntity>? OnAlertStation;

    private void InitializeAdditionalAlertLevelControls()
    {
        AlertStationButton.OnItemSelected += args =>
        {
            if (AlertStationButton.GetItemMetadata(args.Id) is not NetEntity station)
                return;

            AlertStationButton.Select(args.Id);
            OnAlertStation?.Invoke(station);
        };
    }

    // Fish-start: метод обновления состояния дополнительных кодов и станций
    public void UpdateAdditionalAlertLevelState(
        CommunicationsConsoleInterfaceState commsState,
        bool hasAccess)
    {
        UpdateAlertStations(
            commsState.AlertStations,
            commsState.SelectedAlertStation,
            hasAccess);
        UpdateAdditionalAlertLevels(commsState.AdditionalAlertLevels, hasAccess);
    }
    // Fish-end

    /// <summary>
    /// Rebuilds the independently selectable additional alert-level controls.
    /// </summary>
    public void UpdateAdditionalAlertLevels(
        List<CommunicationsConsoleAdditionalAlertLevelState> alerts,
        bool hasAccess)
    {
        AdditionalAlertLevelsContainer.RemoveAllChildren();
        foreach (var alert in alerts)
        {
            // Fish-start: получение локализованного названия кода тревоги
            var name = alert.Level.Id;
            if (_protoManager.TryIndex(alert.Level, out var proto))
                name = proto.LocalizedName;
            else if (_loc.TryGetString($"alert-level-{alert.Level.Id.ToLowerInvariant()}", out var localizedName))
                name = localizedName;
            // Fish-end

            var checkBox = new CheckBox
            {
                Text = name,
                ToggleMode = true,
                Pressed = alert.Enabled,
                Disabled = !hasAccess || !alert.Selectable,
                HorizontalExpand = true,
                Margin = new Thickness(2),
            };

            checkBox.OnToggled += args =>
            {
                checkBox.Disabled = true;
                OnAdditionalAlertLevel?.Invoke(alert.Level, args.Pressed);
            };
            AdditionalAlertLevelsContainer.AddChild(checkBox);
        }

        AdditionalAlertLevelsSection.Visible = alerts.Count > 0;
    }

    /// <summary>
    /// Rebuilds the list of stations available as alert-level targets.
    /// </summary>
    public void UpdateAlertStations(
        List<CommunicationsConsoleAlertStationState> stations,
        NetEntity? selectedStation,
        bool hasAccess)
    {
        AlertStationButton.Clear();
        foreach (var station in stations)
        {
            AlertStationButton.AddItem(station.Name);
            AlertStationButton.SetItemMetadata(AlertStationButton.ItemCount - 1, station.Station);

            if (station.Station == selectedStation)
                AlertStationButton.Select(AlertStationButton.ItemCount - 1);
        }

        AlertStationSelectorContainer.Visible = stations.Count > 0;
        AlertStationButton.Disabled = !hasAccess || stations.Count < 2;
    }

    /// <summary>
    /// Disables alert-level controls while a server-confirmed state is pending.
    /// </summary>
    public void DisableAlertLevelControls()
    {
        // Fish-start: отключение элементов управления в обновлённом AlertLevelControls
        // AlertLevelButton.Disabled = true;
        AlertLevelControls.AlertLevelSelector.Disabled = true;
        AlertLevelControls.ConfirmAlertLevelButton.Disabled = true;
        // Fish-end
        foreach (var child in AdditionalAlertLevelsContainer.Children)
        {
            if (child is CheckBox checkBox)
                checkBox.Disabled = true;
        }
    }
}
