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

        _window = this.CreateWindow<BluespaceArtilleryConsoleWindow>();
        _window.SetEntity(Owner);
        _window.OnFire += () => SendMessage(new BluespaceArtilleryFireMessage());
        _window.OnStationSelected += station => SendMessage(new BluespaceArtillerySelectTargetStationMessage(station));
        _window.OnObjectSelected += target => SendMessage(new BluespaceArtillerySelectTargetObjectMessage(target));
        _window.OnCoordsChanged += coords => SendMessage(new BluespaceArtillerySetCoordsMessage { Coordinates = coords });
        _window.OnParamsChanged += (total, slope, max) =>
            SendMessage(new BluespaceArtillerySetParamsMessage
            {
                ExplosionType = SharedBluespaceArtillerySystem.DefaultExplosionType,
                TotalIntensity = total,
                Slope = slope,
                MaxIntensity = max
            });
        _window.OnPreviewToggled += enabled => SendMessage(new BluespaceArtilleryPreviewMessage { Enabled = enabled });
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is BluespaceArtilleryConsoleBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }
}
