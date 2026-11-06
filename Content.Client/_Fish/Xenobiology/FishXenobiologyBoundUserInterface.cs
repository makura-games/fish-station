using Content.Shared._Fish.Xenobiology;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Fish.Xenobiology;

[UsedImplicitly]
public sealed class FishXenobiologyBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private FishXenobiologyWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<FishXenobiologyWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is FishXenobiologyBoundUserInterfaceState laboratory)
            _window?.UpdateState(laboratory);
    }
}
