using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Shared.Inventory;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Maths;

namespace Content.Client._Fish.DmArena;

public static class DmArenaPalette
{
    public static StyleBoxTexture Rounded(Color color, float horizontal = 12, float vertical = 6)
    {
        var texture = IoCManager.Resolve<IResourceCache>().GetTexture("/Textures/Interface/Nano/button.svg.96dpi.png");
        var box = new StyleBoxTexture { Texture = texture, Modulate = color };
        box.SetPatchMargin(StyleBox.Margin.All, 10);
        box.SetContentMarginOverride(StyleBox.Margin.Horizontal, horizontal);
        box.SetContentMarginOverride(StyleBox.Margin.Vertical, vertical);
        return box;
    }

    public static Font GetFont(bool bold, int size)
    {
        return IoCManager.Resolve<IResourceCache>().NotoStack(bold ? "Bold" : "Regular", size);
    }

    public static readonly Color BodyBg = Color.FromHex("#1A1C1E");
    public static readonly Color SideBg = Color.FromHex("#151719");
    public static readonly Color TitleBarBg = Color.FromHex("#1F2124");
    public static readonly Color Divider = Color.FromHex("#2A2D31");

    public static readonly Color CardBg = Color.FromHex("#232629");
    public static readonly Color CardHoverBg = Color.FromHex("#2C3E50B3");
    public static readonly Color CardSelectedBg = Color.FromHex("#2B3A4D");
    public static readonly Color CardLockedBg = Color.FromHex("#262224");

    public static readonly Color Accent = Color.FromHex("#4A6984");
    public static readonly Color AccentSoft = Color.FromHex("#8FA9C4");

    public static readonly Color ButtonBg = Color.FromHex("#2B3A4A");
    public static readonly Color ButtonHoverBg = Color.FromHex("#3D5A80");
    public static readonly Color ButtonSelectedBg = Color.FromHex("#4A6984");
    public static readonly Color ButtonDisabledBg = Color.FromHex("#26282B");
    public static readonly Color ButtonDisabledFg = Color.FromHex("#6B7075");
    public static readonly Color ButtonText = Color.FromHex("#F2F2F2");

    public static readonly Color PrimaryTop = Color.FromHex("#3D5A80");
    public static readonly Color PrimaryBottom = Color.FromHex("#3A4A78");

    public static readonly Color TextMuted = Color.FromHex("#9AAAAA");
    public static readonly Color Cost = Color.FromHex("#9AAAAA");
    public static readonly Color BalanceWarn = Color.FromHex("#E07A5F");

    public static Color Approach(Color from, Color to, float frameTime)
    {
        var next = Color.InterpolateBetween(from, to, Math.Clamp(frameTime * 14f, 0f, 1f));
        var diff = Math.Abs(next.R - to.R) + Math.Abs(next.G - to.G) + Math.Abs(next.B - to.B) + Math.Abs(next.A - to.A);
        return diff < 0.01f ? to : next;
    }
}
