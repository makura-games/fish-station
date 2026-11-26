using Content.Shared._Fish.Netrunner;
using Robust.Client.UserInterface;

namespace Content.Client._Fish.Netrunner.UI;

/// <summary>Entity-bound shop owned by the implanted operator.</summary>
public sealed class NetrunnerShopBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private NetrunnerShopWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<NetrunnerShopWindow>();
        _window.ProgramRequested += id => SendMessage(new NetrunnerBuyProgramMessage(id));
        _window.UpgradeRequested += upgrade => SendMessage(new NetrunnerBuyUpgradeMessage(upgrade));
        _window.VisorRequested += () => SendMessage(new NetrunnerBuyVisorMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is NetrunnerShopState snapshot)
            _window?.UpdateState(snapshot);
    }
}
