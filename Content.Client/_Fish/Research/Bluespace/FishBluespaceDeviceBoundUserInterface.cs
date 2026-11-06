using Content.Shared._Fish.Research.Bluespace;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Fish.Research.Bluespace;

[UsedImplicitly]
public sealed class FishBluespaceDeviceBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private FishBluespaceDeviceWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<FishBluespaceDeviceWindow>();
        _window.EnabledRequested += enabled => SendMessage(new FishBluespaceSetEnabledMessage(enabled));
        _window.PowerModeRequested += mode => SendMessage(new FishBluespaceSetPowerModeMessage(mode));
        _window.LootCategoryRequested += category => SendMessage(new FishBluespaceSetLootCategoryMessage(category));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is FishBluespaceDeviceBoundUserInterfaceState device)
            _window?.UpdateState(device);
    }
}
