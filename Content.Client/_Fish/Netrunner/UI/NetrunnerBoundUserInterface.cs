using Content.Shared._Fish.Netrunner;
using Robust.Client.UserInterface;

namespace Content.Client._Fish.Netrunner.UI;

public sealed class NetrunnerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private NetrunnerWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<NetrunnerWindow>();
        _window.CameraSelected += camera => SendMessage(new NetrunnerSelectCameraMessage(camera));
        _window.RefreshRequested += () => SendMessage(new NetrunnerRefreshMessage());
        _window.DisconnectRequested += () => SendMessage(new NetrunnerDisconnectMessage());
        _window.ShopRequested += () => SendMessage(new NetrunnerOpenShopMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is NetrunnerUiState snapshot)
            _window?.UpdateState(snapshot);
    }
}
