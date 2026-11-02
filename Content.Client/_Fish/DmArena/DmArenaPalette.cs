using Content.Client.Resources;
using Content.Client.Stylesheets;
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

    public static readonly Color BodyBg = Color.FromHex("#070D14");
    public static readonly Color SideBg = Color.FromHex("#050A10");
    public static readonly Color TitleBarBg = Color.FromHex("#0C1620");
    public static readonly Color Divider = Color.FromHex("#1D3A4C");

    public static readonly Color CardBg = Color.FromHex("#0F1C28");
    public static readonly Color CardHoverBg = Color.FromHex("#1B3A52B3");
    public static readonly Color CardSelectedBg = Color.FromHex("#143A34");
    public static readonly Color CardLockedBg = Color.FromHex("#1A1418");

    public static readonly Color Accent = Color.FromHex("#58AFC4");
    public static readonly Color AccentDim = Color.FromHex("#26566A");
    public static readonly Color CostPillBg = Color.FromHex("#12303A");
    public static readonly Color AccentSoft = Color.FromHex("#8CC8D8");

    public static readonly Color ButtonBg = Color.FromHex("#142C40");
    public static readonly Color ButtonHoverBg = Color.FromHex("#1F4766");
    public static readonly Color ButtonSelectedBg = Color.FromHex("#2A6A78");
    public static readonly Color ButtonDisabledBg = Color.FromHex("#121A22");
    public static readonly Color ButtonDisabledFg = Color.FromHex("#566873");
    public static readonly Color ButtonText = Color.FromHex("#EEF3F6");

    public static readonly Color PrimaryTop = Color.FromHex("#2F8C94");

    public static readonly Color TextMuted = Color.FromHex("#7F9AAB");
    public static readonly Color Cost = Color.FromHex("#E6EEF2");
    public static readonly Color BalanceWarn = Color.FromHex("#D98A6C");

    public static Color Approach(Color from, Color to, float frameTime)
    {
        var next = Color.InterpolateBetween(from, to, Math.Clamp(frameTime * 14f, 0f, 1f));
        var diff = Math.Abs(next.R - to.R) + Math.Abs(next.G - to.G) + Math.Abs(next.B - to.B) + Math.Abs(next.A - to.A);
        return diff < 0.01f ? to : next;
    }
}
