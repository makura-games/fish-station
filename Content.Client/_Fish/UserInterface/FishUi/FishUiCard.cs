using Content.Client._Fish.UserInterface.Crt;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Fish.UserInterface.FishUi;

/// <summary>
/// Карточка контента Fish UI: скруглённая surface-панель под списки/строки.
/// </summary>
public sealed class FishUiCard : Control
{
    private readonly FishCrtPanel _panel;
    public BoxContainer Body { get; }

    public FishUiCard()
    {
        HorizontalExpand = true;
        Margin = new Thickness(0, 0, 0, 2);

        _panel = new FishCrtPanel
        {
            Variant = FishCrtPanelVariant.Surface,
            Rounded = true,
            Effects = FishCrtEffects.None,
            BackgroundOpacity = 0.78f,
            BorderThickness = 1,
            HorizontalExpand = true,
        };

        Body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 3,
            Margin = new Thickness(10, 8),
        };

        _panel.AddChild(Body);
        AddChild(_panel);
    }

    public void SetHighlighted(bool highlighted)
    {
        var context = FishCrtThemeHelpers.FindContext(this);
        _panel.BackgroundOpacity = highlighted ? 0.92f : 0.78f;
        _panel.BorderThickness = highlighted ? 2 : 1;
        _panel.SetColorOverrides(
            null,
            highlighted ? context.Palette.Border : context.Palette.Muted.WithAlpha(0.35f));
    }
}
