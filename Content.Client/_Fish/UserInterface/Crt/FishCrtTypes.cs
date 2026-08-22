using Robust.Client.Graphics;

namespace Content.Client._Fish.UserInterface.Crt;

[Flags]
public enum FishCrtEffects
{
    None = 0,
    HorizontalScanlines = 1 << 0,
    RgbSubpixels = 1 << 1,
    DiagonalStripes = 1 << 2,
}

public enum FishCrtPalettePreset
{
    Blue,
    Brown,
    Custom,
    Green,
    Purple,
    Red,
    /// <summary>
    /// Голубо-серая палитра ближе к Nano/Sunrise, без «морского терминала».
    /// </summary>
    Slate,
    /// <summary>
    /// Тёмная станционная палитра для окна достижений (navy + cyan).
    /// </summary>
    Station,
    Spp,
    White,
    Yellow,
}

public enum FishCrtPanelVariant
{
    Inset,
    Surface,
    Transparent,
    Warning,
}

public enum FishCrtButtonVariant
{
    Danger,
    Filled,
    Navigation,
    Outline,
}

public enum FishCrtContentAlignment
{
    Center,
    Left,
    Right,
}

public enum FishCrtTone
{
    Default,
    Danger,
    Good,
    Muted,
    Warning,
}

public enum FishCrtSeparatorOrientation
{
    Horizontal,
    Vertical,
}

internal readonly record struct FishCrtAppearanceSettings(bool ThemeEnabled, bool EffectsEnabled)
{
    public FishCrtEffects ResolveEffects(FishCrtEffects requested)
    {
        return ThemeEnabled && EffectsEnabled ? requested : FishCrtEffects.None;
    }
}

internal readonly record struct FishCrtThemeContext(
    FishCrtPalette Palette,
    FishCrtAppearanceSettings Appearance)
{
    public bool ThemeEnabled => Appearance.ThemeEnabled;

    public FishCrtEffects ResolveEffects(FishCrtEffects requested)
    {
        return Appearance.ResolveEffects(requested);
    }
}

public readonly record struct FishCrtPalette(
    Color Foreground,
    Color Background,
    Color Border,
    Color Fill,
    Color FillForeground,
    Color Good,
    Color Warning,
    Color Danger,
    Color Muted,
    Color DisabledBackground,
    Color DisabledForeground)
{
    public Color HoverBackground => Color.InterpolateBetween(Background, Fill, 0.3f);

    public Color PressedBackground => Color.InterpolateBetween(Background, Fill, 0.65f);
}

public static class FishCrtPalettes
{
    private static readonly Color Good = Color.FromHex("#00C957");
    private static readonly Color Warning = Color.FromHex("#D3B400");
    private static readonly Color Danger = Color.FromHex("#F04B43");

    public static FishCrtPalette Get(FishCrtPalettePreset preset)
    {
        return preset switch
        {
            FishCrtPalettePreset.Blue => Create("#8ACBFF", "#00000F", "#82C5F2"),
            FishCrtPalettePreset.Brown => Create("#AC8710", "#0F0F00", "#AC8710"),
            FishCrtPalettePreset.Green => Create("#00EB4E", "#001000", "#00EB4E"),
            FishCrtPalettePreset.Purple => Create("#C634D0", "#100302", "#C634D0"),
            FishCrtPalettePreset.Red => Create("#D03434", "#100302", "#D03434"),
            // Голубо-серый: мягче Nano, без «квадратного терминала»
            FishCrtPalettePreset.Slate => CreateSlate(),
            FishCrtPalettePreset.Station => CreateStation(),
            FishCrtPalettePreset.Spp => Create("#DBBF23", "#511814", "#DBBF23"),
            FishCrtPalettePreset.White => Create("#CCCCCC", "#666666", "#CCCCCC"),
            FishCrtPalettePreset.Yellow => Create("#FFD000", "#101000", "#FFD000"),
            _ => Create("#8ACBFF", "#00000F", "#82C5F2"),
        };
    }

    private static FishCrtPalette CreateSlate()
    {
        var foreground = Color.FromHex("#D5DCE8");
        var background = Color.FromHex("#2A2E38");
        var border = Color.FromHex("#6E7A90");
        var fill = Color.FromHex("#5C6B88");
        return new FishCrtPalette(
            foreground,
            background,
            border,
            fill,
            Color.FromHex("#F2F5FA"),
            Color.FromHex("#6FBE84"),
            Color.FromHex("#D0AE6A"),
            Color.FromHex("#D06A6A"),
            Color.FromHex("#9AA3B5"),
            Color.FromHex("#343844"),
            Color.FromHex("#858E9F"));
    }

    private static FishCrtPalette CreateStation()
    {
        return new FishCrtPalette(
            Color.FromHex("#E3ECF5"),
            Color.FromHex("#121820"),
            Color.FromHex("#5EC4E8"),
            Color.FromHex("#2A3544"),
            Color.FromHex("#F2F8FC"),
            Color.FromHex("#6FBE84"),
            Color.FromHex("#D0AE6A"),
            Color.FromHex("#D06A6A"),
            Color.FromHex("#8FA0B8"),
            Color.FromHex("#1A2230"),
            Color.FromHex("#6B7A90"));
    }

    private static FishCrtPalette Create(string foreground, string background, string fill)
    {
        var foregroundColor = Color.FromHex(foreground);
        return new FishCrtPalette(
            foregroundColor,
            Color.FromHex(background),
            foregroundColor,
            Color.FromHex(fill),
            Color.FromHex(background),
            Good,
            Warning,
            Danger,
            Color.InterpolateBetween(Color.FromHex(background), foregroundColor, 0.48f),
            Color.FromHex(background).WithAlpha(0.5f),
            Color.InterpolateBetween(Color.FromHex(background), foregroundColor, 0.42f));
    }
}

public static class FishCrtIcons
{
    public const string Ban = "ban";
    public const string Bullhorn = "bullhorn";
    public const string Cog = "cog";
    public const string DoorOpen = "door_open";
    public const string Heartbeat = "heartbeat";
    public const string Home = "home";
    public const string IdCard = "id_card";
    public const string Map = "map";
    public const string Medal = "medal";
    public const string PaperPlane = "paper_plane";
    public const string Users = "users";
    public const string Warning = "warning";
}

public static class FishCrtStyleClasses
{
    public const string CompactText = "FishCrtCompactText";
    public const string Heading = "FishCrtHeading";
    public const string Text = "FishCrtText";
}
