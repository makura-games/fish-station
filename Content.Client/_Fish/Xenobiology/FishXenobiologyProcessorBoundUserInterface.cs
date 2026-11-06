using Content.Shared._Fish.Xenobiology;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Fish.Xenobiology;

[UsedImplicitly]
public sealed class FishXenobiologyProcessorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private FishXenobiologyProcessorWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<FishXenobiologyProcessorWindow>();
        _window.ProcessRequested += () => SendMessage(new FishXenobiologyProcessMessage());
        _window.EjectRequested += () => SendMessage(new FishXenobiologyEjectMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is FishXenobiologyProcessorBoundUserInterfaceState processor)
            _window?.UpdateState(processor);
    }
}
