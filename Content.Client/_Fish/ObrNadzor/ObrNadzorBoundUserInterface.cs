using Content.Client._Starlight;
using Content.Shared._Fish.ObrNadzor;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controls;
using static Content.Shared.Pinpointer.SharedNavMapSystem;

namespace Content.Client._Fish.ObrNadzor;

[UsedImplicitly]
public sealed class ObrNadzorBoundUserInterface : BoundUserInterface
{
    private ObrNadzorWindow? _window;
    private ObrNadzorConfirmWindow? _confirm;
    private int? _station;

    public ObrNadzorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        UpdateState(State);
    }

    protected override void UpdateState(BoundUserInterfaceState? state)
    {
        if (state is ObrNadzorBuiState s)
            Update(s);
    }

    private void Update(ObrNadzorBuiState state)
    {
        TryInitWindow();
        RefreshUI(state);

        if (state.Observing || state.SessionActive)
            View(ViewType.Control, state);
        else
            View(ViewType.Stations, state);

        if (!_window!.IsOpen)
            _window.OpenCentered();
    }

    private void TryInitWindow()
    {
        if (_window != null)
            return;

        _window = new ObrNadzorWindow();
        _window.OnClose += Close;
        _window.Title = Loc.GetString("obr-nadzor-window-title");

        _window.StationsButton.OnPressed += _ =>
        {
            _station = null;
            if (State is ObrNadzorBuiState state)
                View(ViewType.Stations, state);
        };

        _window.BeaconsButton.OnPressed += _ =>
        {
            if (State is ObrNadzorBuiState state)
                View(ViewType.Beacons, state);
        };

        _window.ControlButton.OnPressed += _ =>
        {
            if (State is ObrNadzorBuiState state)
                View(ViewType.Control, state);
        };

        _window.TakeoverButton.OnPressed += _ => OpenConfirm();
        _window.StopObserveButton.OnPressed += _ => SendMessage(new ObrNadzorStopObserveBuiMsg());
        _window.ReleaseButton.OnPressed += _ => SendMessage(new ObrNadzorReleaseSessionBuiMsg());
    }

    private void OpenConfirm()
    {
        _confirm?.Close();
        _confirm = new ObrNadzorConfirmWindow();
        _confirm.ConfirmText.SetMessage(Loc.GetString("obr-nadzor-confirm-text"));
        _confirm.CancelButton.OnPressed += _ => _confirm.Close();
        _confirm.ConfirmButton.OnPressed += _ =>
        {
            SendMessage(new ObrNadzorTakeoverBuiMsg());
            _confirm.Close();
        };
        _confirm.OpenCentered();
    }

    private void OnStationPressed(int station, List<NavMapBeacon> beacons)
    {
        if (_window == null)
            return;

        _window.Beacons.RemoveAllChildren();
        _station = station;

        foreach (var beacon in beacons)
        {
            var beaconButton = new ChoiceControl();
            beaconButton.Set(beacon.Text ?? Loc.GetString("obr-nadzor-ui-unnamed-beacon"), null);
            beaconButton.Button.Modulate = beacon.Color;
            beaconButton.Button.OnPressed += _ =>
            {
                SendMessage(new ObrNadzorBeaconChosenBuiMsg
                {
                    Beacon = beacon,
                    StationId = station,
                });
            };
            _window.Beacons.AddChild(beaconButton);
        }

        if (State is ObrNadzorBuiState state)
            View(ViewType.Beacons, state);
    }

    private void RefreshUI(ObrNadzorBuiState state)
    {
        if (_window == null)
            return;

        _window.Stations.RemoveAllChildren();

        foreach (var station in state.Stations)
        {
            var stationButton = new ChoiceControl();
            stationButton.Set(station.Value.Name, null);
            stationButton.Button.OnPressed += _ => OnStationPressed(station.Key, station.Value.Beacons);
            _window.Stations.AddChild(stationButton);

            if (station.Key == _station)
                OnStationPressed(station.Key, station.Value.Beacons);
        }

        _window.TakeoverButton.Disabled = !state.Observing || state.SessionActive;
        _window.StopObserveButton.Disabled = !state.Observing;
        _window.ReleaseButton.Disabled = !state.SessionActive;
        _window.ControlStatus.Text = state.SessionActive
            ? Loc.GetString("obr-nadzor-status-session")
            : state.Observing
                ? Loc.GetString("obr-nadzor-status-observing")
                : Loc.GetString("obr-nadzor-status-idle");
    }

    private void View(ViewType type, ObrNadzorBuiState state)
    {
        if (_window == null)
            return;

        _window.Stations.Visible = type == ViewType.Stations;
        _window.Beacons.Visible = type == ViewType.Beacons;
        _window.ControlPanel.Visible = type == ViewType.Control;

        _window.BeaconsButton.Disabled = _station == null && !state.Observing;
        _window.Title = type switch
        {
            ViewType.Beacons when _station != null && state.Stations.TryGetValue(_station.Value, out var station)
                => Loc.GetString("obr-nadzor-window-station", ("station", station.Name)),
            ViewType.Control => Loc.GetString("obr-nadzor-window-control"),
            _ => Loc.GetString("obr-nadzor-window-title"),
        };
    }

    private enum ViewType : byte
    {
        Stations,
        Beacons,
        Control,
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _confirm?.Close();
        _window?.Close();
    }
}
