using Content.Client.Resources;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Utility;

namespace Content.Client._Fish.UserInterface.Crt;

public sealed class FishCrtPanel : PanelContainer, IFishCrtThemedControl
{
    private static readonly ResPath RoundedTexPath = new("/Textures/Interface/Nano/rounded_button.svg.96dpi.png");
    private static readonly ResPath RoundedBorderedTexPath = new("/Textures/Interface/Nano/rounded_button_bordered.svg.96dpi.png");

    private readonly FishCrtEffectRenderer _effectsRenderer = new();
    private readonly StyleBoxFlat _crtStyle = new();
    private readonly StyleBoxFlat _nanoWarningStyle = new();
    private readonly StyleBoxTexture _roundedStyle = new();
    private readonly IResourceCache _resourceCache;

    private Color? _backgroundOverride;
    private Color? _borderOverride;
    private float _backgroundOpacity = 0.72f;
    private float _borderThickness = 1;
    private bool _rounded;
    private bool _roundedTexturesReady;
    private FishCrtThemeContext _context = new(
        FishCrtPalettes.Get(FishCrtPalettePreset.Blue),
        new FishCrtAppearanceSettings(true, true));
    private FishCrtPanelVariant _variant = FishCrtPanelVariant.Surface;

    public FishCrtEffects Effects { get; set; }
    public float BackgroundOpacity
    {
        get => _backgroundOpacity;
        set
        {
            _backgroundOpacity = value;
            UpdateStyle();
        }
    }
    public float RgbOpacity { get; set; } = 0.06f;
    public float RgbWidth { get; set; } = 1;
    public float ScanlineOpacity { get; set; } = 0.25f;
    public float ScanlineSpacing { get; set; } = 2;
    public float ScanlineThickness { get; set; } = 1;
    public float StripeWidth { get; set; } = 18;

    /// <summary>
    /// Мягкие скруглённые углы через Nano StyleBoxTexture (вместо квадратного StyleBoxFlat).
    /// </summary>
    public bool Rounded
    {
        get => _rounded;
        set
        {
            if (_rounded == value)
                return;

            _rounded = value;
            UpdateStyle();
        }
    }

    public float BorderThickness
    {
        get => _borderThickness;
        set
        {
            _borderThickness = value;
            UpdateStyle();
        }
    }

    public FishCrtPanelVariant Variant
    {
        get => _variant;
        set
        {
            _variant = value;
            UpdateStyle();
        }
    }

    public FishCrtPanel()
    {
        _resourceCache = IoCManager.Resolve<IResourceCache>();
        PanelOverride = _crtStyle;
        UpdateStyle();
    }

    void IFishCrtThemedControl.ApplyCrtTheme(FishCrtThemeContext context)
    {
        ApplyAppearance(context);
    }

    internal void ApplyAppearance(FishCrtThemeContext context)
    {
        _context = context;
        UpdateStyle();
    }

    internal void SetColorOverrides(Color? background, Color? border)
    {
        _backgroundOverride = background;
        _borderOverride = border;
        UpdateStyle();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        ApplyAppearance(FishCrtThemeHelpers.FindContext(this));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        _effectsRenderer.Draw(
            handle,
            PixelWidth,
            PixelHeight,
            UIScale,
            _context.ResolveEffects(Effects),
            ScanlineSpacing,
            ScanlineThickness,
            RgbWidth,
            StripeWidth,
            ScanlineOpacity,
            RgbOpacity,
            _context.Palette.Background.WithAlpha(0.3f));
    }

    private void EnsureRoundedTextures()
    {
        if (_roundedTexturesReady)
            return;

        _roundedStyle.SetPatchMargin(StyleBox.Margin.All, 10);
        _roundedStyle.SetPadding(StyleBox.Margin.All, 1);
        _roundedTexturesReady = true;
    }

    private void UpdateStyle()
    {
        RemoveStyleClass(StyleNano.StyleClassBorderedWindowPanel);
        RemoveStyleClass(StyleClass.BackgroundPanelDark);

        if (!_context.ThemeEnabled)
        {
            UpdateNanoStyle();
            return;
        }

        var palette = _context.Palette;
        var background = Variant switch
        {
            FishCrtPanelVariant.Inset => palette.Background.WithAlpha(Math.Clamp(BackgroundOpacity + 0.15f, 0, 1)),
            FishCrtPanelVariant.Surface => palette.Background.WithAlpha(Math.Clamp(BackgroundOpacity, 0, 1)),
            FishCrtPanelVariant.Transparent => Color.Transparent,
            FishCrtPanelVariant.Warning => palette.Warning.WithAlpha(0.72f),
            _ => palette.Background.WithAlpha(Math.Clamp(BackgroundOpacity, 0, 1)),
        };
        var border = Variant == FishCrtPanelVariant.Warning ? palette.Warning : palette.Border;
        background = _backgroundOverride ?? background;
        border = _borderOverride ?? border;

        if (Rounded && Variant != FishCrtPanelVariant.Transparent)
        {
            EnsureRoundedTextures();
            var useBorder = BorderThickness > 0.01f;
            _roundedStyle.Texture = _resourceCache.GetTexture(useBorder ? RoundedBorderedTexPath : RoundedTexPath);
            // StyleBoxTexture красит всю плашку одним Modulate — берём фон; рамка уже в текстуре.
            _roundedStyle.Modulate = background.A <= 0.001f ? border.WithAlpha(0.85f) : background;
            PanelOverride = _roundedStyle;
            return;
        }

        // Для кнопок с override-цветами и Rounded=true Variant часто Transparent.
        if (Rounded && (_backgroundOverride != null || _borderOverride != null))
        {
            EnsureRoundedTextures();
            var useBorder = BorderThickness > 0.01f;
            _roundedStyle.Texture = _resourceCache.GetTexture(useBorder ? RoundedBorderedTexPath : RoundedTexPath);
            var fill = _backgroundOverride ?? background;
            if (fill.A <= 0.001f)
                fill = (_borderOverride ?? border).WithAlpha(0.35f);
            _roundedStyle.Modulate = fill;
            PanelOverride = _roundedStyle;
            return;
        }

        PanelOverride = _crtStyle;
        _crtStyle.BackgroundColor = background;
        _crtStyle.BorderColor = border;
        _crtStyle.BorderThickness = new Thickness(BorderThickness);
    }

    private void UpdateNanoStyle()
    {
        switch (Variant)
        {
            case FishCrtPanelVariant.Inset:
                PanelOverride = null;
                AddStyleClass(StyleClass.BackgroundPanelDark);
                break;
            case FishCrtPanelVariant.Surface:
                PanelOverride = null;
                AddStyleClass(StyleNano.StyleClassBorderedWindowPanel);
                break;
            case FishCrtPanelVariant.Transparent:
                PanelOverride = null;
                break;
            case FishCrtPanelVariant.Warning:
                _nanoWarningStyle.BackgroundColor = StyleNano.PanelDark;
                _nanoWarningStyle.BorderColor = StyleNano.ConcerningOrangeFore;
                _nanoWarningStyle.BorderThickness = new Thickness(BorderThickness);
                PanelOverride = _nanoWarningStyle;
                break;
        }
    }
}
