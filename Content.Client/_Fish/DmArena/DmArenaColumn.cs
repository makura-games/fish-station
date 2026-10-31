using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._Fish.DmArena;

public sealed class DmArenaColumn : PanelContainer
{
    private readonly ScrollContainer _scroll;
    private readonly PanelContainer _sticky;
    private readonly Label _stickyLabel;
    private readonly List<(Label Header, string Text)> _headers = new();

    public BoxContainer Items { get; }

    public DmArenaColumn(Color background)
    {
        HorizontalExpand = true;
        VerticalExpand = true;
        MinWidth = 250;
        SizeFlagsStretchRatio = 1.5f;
        PanelOverride = DmArenaPalette.Rounded(background, 4, 4);

        Items = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 6,
            Margin = new Thickness(6, 4, 14, 4),
        };

        _scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
        };
        _scroll.AddChild(Items);
        AddChild(_scroll);

        _stickyLabel = new Label
        {
            FontOverride = DmArenaPalette.GetFont(true, 14),
            FontColorOverride = DmArenaPalette.ButtonText,
            Margin = new Thickness(6, 0, 0, 0),
        };
        _sticky = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Top,
            Visible = false,
            MouseFilter = MouseFilterMode.Ignore,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = DmArenaPalette.TitleBarBg,
                BorderColor = DmArenaPalette.Accent,
                BorderThickness = new Thickness(0, 0, 0, 1),
                ContentMarginTopOverride = 8,
                ContentMarginBottomOverride = 8,
            },
        };
        _sticky.AddChild(_stickyLabel);
        AddChild(_sticky);
    }

    public void Clear()
    {
        Items.RemoveAllChildren();
        _headers.Clear();
        _sticky.Visible = false;
    }

    public void AddHeader(string text)
    {
        var header = new Label
        {
            Text = text,
            FontOverride = DmArenaPalette.GetFont(true, 14),
            FontColorOverride = DmArenaPalette.AccentSoft,
            Margin = new Thickness(6, 14, 0, 4),
        };
        _headers.Add((header, text));
        Items.AddChild(header);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var scrolled = _scroll.GetScrollValue().Y;
        string? current = null;

        foreach (var (header, text) in _headers)
        {
            var passed = header.Position.Y + header.Margin.Top < scrolled;
            header.FontColorOverride = passed ? DmArenaPalette.TextMuted : DmArenaPalette.AccentSoft;

            if (passed)
                current = text;
        }

        _sticky.Visible = current != null;
        if (current != null)
            _stickyLabel.Text = current;
    }
}
