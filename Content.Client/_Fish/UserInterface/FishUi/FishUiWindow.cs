using Content.Client._Fish.UserInterface.Crt;
using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Fish.UserInterface.FishUi;

/// <summary>
/// Базовое окно Fish UI: FancyWindow + Station-палитра CRT без scanline-шума.
/// Сюда сажаем навыки, достижения и следующие Fish-окна — не размазывать ванильный StyleNano.
/// </summary>
public class FishUiWindow : FancyWindow
{
    protected FishCrtThemeScope ThemeScope { get; }
    protected BoxContainer ContentRoot { get; }

    public FishUiWindow()
    {
        ThemeScope = new FishCrtThemeScope
        {
            Palette = FishCrtPalettePreset.Station,
            Effects = FishCrtEffects.None,
            HorizontalExpand = true,
            VerticalExpand = true,
            BorderThickness = 0,
            BackgroundOpacity = 0,
        };

        ContentRoot = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 10,
            Margin = new Thickness(12, 8, 12, 12),
        };

        ThemeScope.AddChild(ContentRoot);
        ContentsContainer.AddChild(ThemeScope);
    }

    protected FishCrtPanel CreateHeaderSurface()
    {
        return new FishCrtPanel
        {
            Variant = FishCrtPanelVariant.Surface,
            Rounded = true,
            Effects = FishCrtEffects.None,
            BackgroundOpacity = 0.65f,
            BorderThickness = 0,
            HorizontalExpand = true,
        };
    }

    protected FishCrtPanel CreateInsetSurface()
    {
        return new FishCrtPanel
        {
            Variant = FishCrtPanelVariant.Inset,
            Rounded = true,
            Effects = FishCrtEffects.None,
            BackgroundOpacity = 0.48f,
            BorderThickness = 0,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
    }
}
