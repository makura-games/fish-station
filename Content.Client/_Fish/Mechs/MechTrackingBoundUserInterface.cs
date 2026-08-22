using Content.Shared._Fish.Mechs;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;

namespace Content.Client._Fish.Mechs;

public sealed class MechTrackingBoundUserInterface : BoundUserInterface
{
    private MechTrackingWindow? _window;

    public MechTrackingBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<MechTrackingWindow>();
        _window.OnRefresh += () => SendMessage(new MechTrackingRefreshMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is MechTrackingBoundUserInterfaceState st)
            _window?.Update(st);
    }
}

public sealed class MechTrackingWindow : DefaultWindow
{
    public event Action? OnRefresh;

    private readonly BoxContainer _list;

    public MechTrackingWindow()
    {
        Title = Loc.GetString("mech-tracking-title");
        MinSize = SetSize = new(420, 360);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var refresh = new Button { Text = Loc.GetString("mech-tracking-refresh") };
        refresh.OnPressed += _ => OnRefresh?.Invoke();
        root.AddChild(refresh);

        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true };
        _list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        scroll.AddChild(_list);
        root.AddChild(scroll);
        Contents.AddChild(root);
    }

    public void Update(MechTrackingBoundUserInterfaceState state)
    {
        _list.RemoveAllChildren();
        foreach (var entry in state.Entries)
        {
            var pilot = string.IsNullOrEmpty(entry.PilotName)
                ? Loc.GetString("mech-tracking-no-pilot")
                : entry.PilotName;
            var status = entry.Broken
                ? Loc.GetString("mech-tracking-broken")
                : Loc.GetString("mech-tracking-ok");
            _list.AddChild(new Label
            {
                Text = Loc.GetString("mech-tracking-entry",
                    ("name", entry.Name),
                    ("integrity", entry.IntegrityPercent.ToString("0")),
                    ("energy", entry.EnergyPercent.ToString("0")),
                    ("pilot", pilot),
                    ("status", status)),
            });
        }
    }
}
