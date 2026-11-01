using Content.Shared._Fish.Xenobiology.Machines;
using Robust.Client.UserInterface;

namespace Content.Client._Fish.Xenobiology.Machines;

public sealed class FishXenobiologyConsoleBoundUserInterface : BoundUserInterface
{
    private FishXenobiologyConsoleWindow? _window;

    public FishXenobiologyConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FishXenobiologyConsoleWindow>();
        _window.OnReleaseAll += () => SendMessage(new FishXenoConsoleReleaseAllMessage());
        _window.OnCollect += () => SendMessage(new FishXenoConsoleCollectSlimesMessage());
        _window.OnScan += () => SendMessage(new FishXenoConsoleScanStoredMessage());
        _window.OnFeed += () => SendMessage(new FishXenoConsoleFeedPotionMessage());
        _window.OnEjectMonkey += () => SendMessage(new FishXenoConsoleEjectMonkeyMessage());
        _window.OnRecycle += () => SendMessage(new FishXenoConsoleRecycleMonkeysMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is FishXenobiologyConsoleBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }
}
