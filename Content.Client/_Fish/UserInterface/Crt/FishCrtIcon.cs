using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Utility;

namespace Content.Client._Fish.UserInterface.Crt;

public sealed class FishCrtIcon : TextureRect, IFishCrtThemedControl
{
    public static readonly ResPath DefaultRsiPath = new("/Textures/_Fish/Interface/CRT/crt_icons.rsi");

    private static readonly ISawmill Sawmill = Logger.GetSawmill("fish-crt");

    private readonly IResourceCache _resourceCache;
    private string? _iconState;
    private ResPath _rsiPath = DefaultRsiPath;
    private FishCrtThemeContext _context = new(
        FishCrtPalettes.Get(FishCrtPalettePreset.Blue),
        new FishCrtAppearanceSettings(true, true));
    private FishCrtTone _tone = FishCrtTone.Default;

    public string? IconState
    {
        get => _iconState;
        set
        {
            _iconState = value;
            UpdateIcon();
        }
    }

    public ResPath RsiPath
    {
        get => _rsiPath;
        set
        {
            _rsiPath = value;
            UpdateIcon();
        }
    }

    public FishCrtTone Tone
    {
        get => _tone;
        set
        {
            _tone = value;
            UpdateColor();
        }
    }

    public FishCrtIcon()
    {
        _resourceCache = IoCManager.Resolve<IResourceCache>();
        Stretch = StretchMode.Scale;
        UpdateColor();
    }

    void IFishCrtThemedControl.ApplyCrtTheme(FishCrtThemeContext context)
    {
        ApplyAppearance(context);
    }

    internal void ApplyAppearance(FishCrtThemeContext context)
    {
        _context = context;
        UpdateColor();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        ApplyAppearance(FishCrtThemeHelpers.FindContext(this));
    }

    private void UpdateColor()
    {
        if (!_context.ThemeEnabled)
        {
            ModulateSelfOverride = Tone switch
            {
                FishCrtTone.Danger => StyleNano.DangerousRedFore,
                FishCrtTone.Good => StyleNano.GoodGreenFore,
                FishCrtTone.Muted => StyleNano.DisabledFore,
                FishCrtTone.Warning => StyleNano.ConcerningOrangeFore,
                _ => null,
            };
            return;
        }

        var palette = _context.Palette;
        ModulateSelfOverride = Tone switch
        {
            FishCrtTone.Danger => palette.Danger,
            FishCrtTone.Good => palette.Good,
            FishCrtTone.Muted => palette.Muted,
            FishCrtTone.Warning => palette.Warning,
            _ => palette.Foreground,
        };
    }

    private void UpdateIcon()
    {
        if (string.IsNullOrWhiteSpace(IconState))
        {
            Texture = null;
            return;
        }

        try
        {
            var rsi = _resourceCache.GetResource<RSIResource>(RsiPath).RSI;
            if (rsi.TryGetState(new RSI.StateId(IconState), out var state))
            {
                Texture = state.Frame0;
                return;
            }

            Texture = null;
            Sawmill.Warning($"CRT icon state '{IconState}' does not exist in '{RsiPath}'.");
        }
        catch (Exception exception)
        {
            Texture = null;
            Sawmill.Error($"Failed to load CRT icon state '{IconState}' from '{RsiPath}': {exception}");
        }
    }
}
