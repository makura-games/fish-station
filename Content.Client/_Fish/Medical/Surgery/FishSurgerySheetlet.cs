using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Content.Client.Stylesheets.Palette;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Fish.Medical.Surgery;

/// <summary>Shared medical colors for the surgery window and anatomical selector.</summary>
[CommonSheetlet]
public sealed class FishSurgerySheetlet : Sheetlet<PalettedStylesheet>
{
    public static readonly ColorPalette Palette = ColorPalette.FromHexBase(
        "#83B6EF", element: Color.FromHex("#83B6EF"),
        background: Color.FromHex("#171D29"), text: Color.FromHex("#E6EAF2")) with
    {
        BackgroundLight = Color.FromHex("#252E40"),
        TextDark = Color.FromHex("#A8B6CA"),
        DisabledElement = Color.FromHex("#445168"),
    };
    public static readonly ColorPalette Danger = ColorPalette.FromHexBase(
        "#C95555", background: Color.FromHex("#361F26"), text: Color.FromHex("#FFA7A0"));

    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var rules = new List<StyleRule>
        {
            E<PanelContainer>().Class("FishSurgeryHeader").Panel(new StyleBoxFlat
            {
                BackgroundColor = Palette.BackgroundLight,
            }),
            E<PanelContainer>().Class("FishSurgeryBackground").Panel(new StyleBoxFlat
            {
                BackgroundColor = Palette.Background,
            }),
            E<Label>().Class("FishSurgeryHeading")
                .Font(sheet.BaseFont.GetFont(13, FontKind.Bold)).FontColor(Palette.Text),
            E<RichTextLabel>().Class("FishSurgeryHeading")
                .Font(sheet.BaseFont.GetFont(13, FontKind.Bold)).FontColor(Palette.Text),
            E<PanelContainer>().Class("FishSurgeryStatusChip").Panel(new StyleBoxFlat
            {
                BackgroundColor = Palette.BackgroundLight,
                BorderColor = Palette.Element,
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2,
            }),
            E<Label>().Class("FishSurgeryStatusText").FontColor(Palette.Element),
            E<Label>().Class("FishSurgeryMuted").FontColor(Palette.TextDark),
            E<RichTextLabel>().Class("FishSurgeryMuted").FontColor(Palette.TextDark),
            E<PanelContainer>().Class("FishSurgeryInset").Panel(Box(Palette.BackgroundLight, Palette.BackgroundLight)),
            E<PanelContainer>().Class("FishSurgeryDangerPanel").Panel(Box(Danger.Background, Danger.Element)),
            E<ProgressBar>().Class("FishSurgeryProgress")
                .Prop(ProgressBar.StylePropertyBackground, new StyleBoxFlat { BackgroundColor = Palette.BackgroundDark })
                .Prop(ProgressBar.StylePropertyForeground, new StyleBoxFlat { BackgroundColor = Palette.Element }),
            E<ProgressBar>().Class("FishSurgeryProgressDanger")
                .Prop(ProgressBar.StylePropertyForeground, new StyleBoxFlat { BackgroundColor = Danger.Element }),
        };
        AddButtonRules(rules, "FishSurgeryButton", Palette);
        AddButtonRules(rules, "FishSurgeryDangerButton", Danger);
        rules.Add(E<Button>().Class("FishSurgeryNext").Box(Box(Palette.BackgroundLight, Palette.Element)));
        rules.Add(E<Button>().Class("FishSurgeryDone").Box(Box(Palette.BackgroundDark, Palette.DisabledElement)));
        return rules.ToArray();
    }

    private static void AddButtonRules(List<StyleRule> rules, string styleClass, ColorPalette palette)
    {
        rules.Add(E<Button>().Class(styleClass).Box(Box(palette.BackgroundLight, palette.BackgroundLight)).MinHeight(32).Modulate(Color.White));
        rules.Add(E<Button>().Class(styleClass).PseudoNormal().Modulate(Color.White));
        rules.Add(E<Button>().Class(styleClass).PseudoHovered().Modulate(Color.White).Box(Box(palette.BackgroundLight, palette.Element)));
        rules.Add(E<Button>().Class(styleClass).PseudoPressed().Modulate(Color.White).Box(Box(palette.DisabledElement, palette.Element)));
        rules.Add(E<Button>().Class(styleClass).PseudoDisabled().Modulate(Color.White).Box(Box(palette.BackgroundDark, palette.DisabledElement)));
    }

    private static StyleBoxFlat Box(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 10,
            ContentMarginRightOverride = 10,
            ContentMarginTopOverride = 6,
            ContentMarginBottomOverride = 6,
        };
    }
}
