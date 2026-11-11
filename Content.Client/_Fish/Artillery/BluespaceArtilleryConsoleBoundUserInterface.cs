using Content.Shared._Fish.Artillery;
using Robust.Client.UserInterface;

namespace Content.Client._Fish.Artillery;

public sealed partial class BluespaceArtilleryConsoleBoundUserInterface : BoundUserInterface
{
    private BluespaceArtilleryConsoleWindow? _window;

    public BluespaceArtilleryConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new BluespaceArtilleryConsoleWindow();
        _window.OnClose += Close;
        _window.OnFire += () => SendMessage(new BluespaceArtilleryFireMessage());
        _window.OnStationSelected += station => SendMessage(new BluespaceArtillerySelectTargetStationMessage(station));
        _window.OnCoordsChanged += coords => SendMessage(new BluespaceArtillerySetCoordsMessage { Coordinates = coords });
        _window.OnParamsChanged += (type, total, slope, max) =>
            SendMessage(new BluespaceArtillerySetParamsMessage
            {
                ExplosionType = type,
                TotalIntensity = total,
                Slope = slope,
                MaxIntensity = max
            });
        _window.OnPreviewToggled += enabled => SendMessage(new BluespaceArtilleryPreviewMessage { Enabled = enabled });

        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is BluespaceArtilleryConsoleBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        if (_window != null)
        {
            _window.OnClose -= Close;
            _window.Close();
            _window = null;
        }
    }
}