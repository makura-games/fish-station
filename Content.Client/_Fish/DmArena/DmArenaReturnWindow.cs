using System.Numerics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Fish.DmArena;

public sealed class DmArenaReturnWindow : DmArenaFrameWindow
{
    public event Action? OnAccepted;

    public DmArenaReturnWindow()
    {
        WindowTitle = Loc.GetString("dm-arena-revival-title");
        SetSize = new Vector2(350, 160);
        Resizable = false;

        var message = new Label
        {
            Text = Loc.GetString("dm-arena-revival-offer"),
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(8, 12),
        };
        Contents.AddChild(message);

        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            SeparationOverride = 8,
            Margin = new Thickness(8, 4),
        };

        var acceptButton = new DmArenaPillButton
        {
            Text = Loc.GetString("dm-arena-revival-accept"),
        };
        acceptButton.OnPressed += () =>
        {
            OnAccepted?.Invoke();
            Close();
        };
        buttons.AddChild(acceptButton);

        var declineButton = new DmArenaPillButton
        {
            Text = Loc.GetString("dm-arena-revival-decline"),
        };
        declineButton.OnPressed += () => Close();
        buttons.AddChild(declineButton);

        Contents.AddChild(buttons);
    }
}
