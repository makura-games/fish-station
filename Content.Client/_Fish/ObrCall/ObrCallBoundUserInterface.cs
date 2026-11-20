using Content.Shared._Fish.ObrCall;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._Fish.ObrCall;

public sealed class ObrCallBoundUserInterface : BoundUserInterface
{
    private ObrCallWindow? _window;

    public ObrCallBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ObrCallWindow>();
        _window.OnCallPressed += OnCallPressed;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ObrCallBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }

    private void OnCallPressed(string teamId, string mission, bool spawnInCryo)
    {
        SendMessage(new ObrCallRequestMessage(teamId, mission, spawnInCryo));
    }
}
