using Content.Shared._Fish.Shipyard;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Fish.Shipyard;

[UsedImplicitly]
public sealed class ShipyardConsoleBoundUserInterface : BoundUserInterface
{
    private ShipyardConsoleWindow? _window;

    public ShipyardConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ShipyardConsoleWindow>();
        _window.OnPurchasePressed += vesselId => SendMessage(new ShipyardConsolePurchaseMessage(vesselId));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ShipyardConsoleBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }
}
