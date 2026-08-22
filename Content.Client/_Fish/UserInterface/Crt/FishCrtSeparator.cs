using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Fish.UserInterface.Crt;

public sealed class FishCrtSeparator : Control, IFishCrtThemedControl
{
    private FishCrtThemeContext _context = new(
        FishCrtPalettes.Get(FishCrtPalettePreset.Blue),
        new FishCrtAppearanceSettings(true, true));
    private FishCrtSeparatorOrientation _orientation;
    private float _thickness = 1;

    internal Color ResolvedColor =>
        _context.ThemeEnabled ? _context.Palette.Border : StyleNano.NanoGold;

    public FishCrtSeparatorOrientation Orientation
    {
        get => _orientation;
        set
        {
            _orientation = value;
            UpdateMinimumSize();
        }
    }

    public float Thickness
    {
        get => _thickness;
        set
        {
            _thickness = value;
            UpdateMinimumSize();
        }
    }

    public FishCrtSeparator()
    {
        UpdateMinimumSize();
    }

    void IFishCrtThemedControl.ApplyCrtTheme(FishCrtThemeContext context)
    {
        ApplyAppearance(context);
    }

    internal void ApplyAppearance(FishCrtThemeContext context)
    {
        _context = context;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        ApplyAppearance(FishCrtThemeHelpers.FindContext(this));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (Orientation == FishCrtSeparatorOrientation.Vertical)
        {
            var left = Math.Max(0, (PixelWidth - Thickness * UIScale) / 2);
            handle.DrawRect(new UIBox2(left, 0, left + Thickness * UIScale, PixelHeight), ResolvedColor);
            return;
        }

        var top = Math.Max(0, (PixelHeight - Thickness * UIScale) / 2);
        handle.DrawRect(new UIBox2(0, top, PixelWidth, top + Thickness * UIScale), ResolvedColor);
    }

    private void UpdateMinimumSize()
    {
        MinSize = Orientation == FishCrtSeparatorOrientation.Vertical
            ? new System.Numerics.Vector2(Thickness, 1)
            : new System.Numerics.Vector2(1, Thickness);
    }
}
