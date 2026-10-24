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

    public static readonly Color Accent = Color.FromHex("#4fd1c5");
    public static readonly Color AccentDim = Color.FromHex("#1f5a5b");

    public static readonly Color BodyBg = Color.FromHex("#04080d");
    public static readonly Color TitleBarBg = Color.FromHex("#0a121b");
    public static readonly Color CardBg = Color.FromHex("#0c1520");
    public static readonly Color CardHoverBg = Color.FromHex("#132434");
    public static readonly Color CardSelectedBg = Color.FromHex("#123a25");

    public static readonly Color CardLockedBg = Color.FromHex("#2a1018");

    public static readonly Color Border = Color.FromHex("#1d4a63");

    public static readonly Color ButtonBg = Color.FromHex("#12324f");
    public static readonly Color ButtonHoverBg = Color.FromHex("#1a4a73");
    public static readonly Color ButtonSelectedBg = Color.FromHex("#1f7a49");
    public static readonly Color ButtonDisabledBg = Color.FromHex("#34161c");
    public static readonly Color ButtonDisabledFg = Color.FromHex("#b07a82");
    public static readonly Color ButtonText = Color.FromHex("#eaf4fb");

    public static readonly Color TextMuted = Color.FromHex("#7f98ab");
    public static readonly Color Cost = Color.FromHex("#f0c36d");
}
