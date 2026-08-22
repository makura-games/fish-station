using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._Fish.UserInterface.Crt;

internal interface IFishCrtThemedControl
{
    void ApplyCrtTheme(FishCrtThemeContext context);
}

internal static class FishCrtThemeHelpers
{
    public static FormattedMessage CreateTextMessage(
        string? text,
        FishCrtThemeContext context,
        int fontSize = 0,
        bool heading = false)
    {
        var message = new FormattedMessage();
        // Fish: обычные Nano-шрифты вместо Monospace (меньше «терминал CM13»)
        var nanoHeadingDefaults = heading && fontSize <= 0;
        var fontId = heading
            ? nanoHeadingDefaults
                ? "DefaultBold"
                : "NotoSansDisplayBold"
            : "Default";
        var resolvedFontSize = nanoHeadingDefaults ? 16 : fontSize;
        Dictionary<string, MarkupParameter>? attributes = null;
        if (resolvedFontSize > 0)
        {
            attributes = new Dictionary<string, MarkupParameter>
            {
                ["size"] = new MarkupParameter(LongValue: resolvedFontSize),
            };
        }

        message.PushTag(new MarkupNode("font", new MarkupParameter(fontId), attributes));
        message.AddText(text ?? string.Empty);
        message.Pop();
        return message;
    }

    public static FishCrtThemeContext FindContext(Control control)
    {
        for (var parent = control.Parent; parent != null; parent = parent.Parent)
        {
            if (parent is FishCrtThemeScope scope)
                return scope.ResolvedContext;
        }

        return new FishCrtThemeContext(
            FishCrtPalettes.Get(FishCrtPalettePreset.Blue),
            new FishCrtAppearanceSettings(true, true));
    }

    public static void ApplyToDescendants(Control control, FishCrtThemeContext context)
    {
        foreach (var child in control.Children)
        {
            if (child is FishCrtThemeScope)
                continue;

            if (child is IFishCrtThemedControl themed)
                themed.ApplyCrtTheme(context);

            ApplyToDescendants(child, context);
        }
    }
}
