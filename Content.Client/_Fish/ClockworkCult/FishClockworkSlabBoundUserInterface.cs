using Content.Shared._Fish.ClockworkCult.UI;
using Robust.Client.UserInterface;

namespace Content.Client._Fish.ClockworkCult;

public sealed class FishClockworkSlabBoundUserInterface : BoundUserInterface
{
    private FishClockworkSlabWindow? _window;

    public FishClockworkSlabBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<FishClockworkSlabWindow>();
        _window.OnRecite += id => SendMessage(new FishClockworkSlabReciteMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is FishClockworkSlabBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }
}
