using Content.Client.Stylesheets;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Fish.UserInterface.Crt;

public sealed class FishCrtLabel : RichTextLabel, IFishCrtThemedControl
{
    private FishCrtThemeContext _context = new(
        FishCrtPalettes.Get(FishCrtPalettePreset.Blue),
        new FishCrtAppearanceSettings(true, true));
    private bool _heading;
    private string? _text;
    private int _textFontSize;
    private FishCrtTone _tone = FishCrtTone.Default;

    public new string? Text
    {
        get => _text;
        set
        {
            if (_text == value)
                return;

            _text = value;
            UpdateAppearance();
        }
    }

    public FishCrtTone Tone
    {
        get => _tone;
        set
        {
            _tone = value;
            UpdateAppearance();
        }
    }

    public bool Heading
    {
        get => _heading;
        set
        {
            _heading = value;
            UpdateAppearance();
        }
    }

    /// <summary>
    /// Overrides only this label's text size. Zero keeps the active theme's default font.
    /// </summary>
    public int TextFontSize
    {
        get => _textFontSize;
        set
        {
            _textFontSize = value;
            UpdateAppearance();
        }
    }

    public FishCrtLabel()
    {
        UpdateAppearance();
    }

    void IFishCrtThemedControl.ApplyCrtTheme(FishCrtThemeContext context)
    {
        ApplyAppearance(context);
    }

    internal void ApplyAppearance(FishCrtThemeContext context)
    {
        _context = context;
        UpdateAppearance();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        ApplyAppearance(FishCrtThemeHelpers.FindContext(this));
    }

    private void UpdateAppearance()
    {
        SetMessage(FishCrtThemeHelpers.CreateTextMessage(
            _text,
            _context,
            TextFontSize,
            Heading));

        var palette = _context.Palette;
        Color? color = Tone switch
        {
            FishCrtTone.Danger => _context.ThemeEnabled ? palette.Danger : StyleNano.DangerousRedFore,
            FishCrtTone.Good => _context.ThemeEnabled ? palette.Good : StyleNano.GoodGreenFore,
            FishCrtTone.Muted => _context.ThemeEnabled ? palette.Muted : StyleNano.DisabledFore,
            FishCrtTone.Warning => _context.ThemeEnabled ? palette.Warning : StyleNano.ConcerningOrangeFore,
            _ => _context.ThemeEnabled
                ? palette.Foreground
                : Heading
                    ? StyleNano.NanoGold
                    : null,
        };
        ModulateSelfOverride = color;
    }
}
