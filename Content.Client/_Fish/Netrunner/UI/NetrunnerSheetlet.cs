using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Content.Client.Stylesheets.Palette;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Fish.Netrunner.UI;

/// <summary>Scoped neon styling for the neural terminal and its script catalog.</summary>
[CommonSheetlet]
public sealed class NetrunnerSheetlet : Sheetlet<PalettedStylesheet>
{
    public const string Frame = "NetrunnerFrame";
    public const string Panel = "NetrunnerPanel";
    public const string Card = "NetrunnerCard";
    public const string Heading = "NetrunnerHeading";
    public const string Caption = "NetrunnerCaption";
    public const string Accent = "NetrunnerAccent";
    public const string Button = "NetrunnerButton";
    public const string DangerButton = "NetrunnerDangerButton";
    public const string Search = "NetrunnerSearch";
    public const string MemoryBar = "NetrunnerMemoryBar";
    public const string TraceBar = "NetrunnerTraceBar";
    public const string CameraList = "NetrunnerCameraList";

    private static readonly ColorPalette Cyan = ColorPalette.FromHexBase("#33E6DF",
        element: Color.FromHex("#15424E"), background: Color.FromHex("#0B1825"),
        text: Color.FromHex("#C9FFFA"));
    private static readonly ColorPalette Magenta = ColorPalette.FromHexBase("#F55AB7",
        element: Color.FromHex("#502242"), background: Color.FromHex("#171224"),
        text: Color.FromHex("#FFD1EF"));

    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var frame = CreateBox(Cyan.BackgroundDark, Cyan.Element, 10);
        var panel = CreateBox(Cyan.Background, Cyan.Element, 8);
        var card = CreateBox(Magenta.Background, Magenta.Element, 10);
        var button = CreateBox(Cyan.Element, Cyan.Base, 6);
        var buttonHover = CreateBox(Cyan.HoveredElement, Cyan.Text, 6);
        var buttonPressed = CreateBox(Cyan.PressedElement, Cyan.Base, 6);
        var buttonDisabled = CreateBox(Cyan.DisabledElement, Cyan.Element, 6);
        var danger = CreateBox(Magenta.Element, Magenta.Base, 6);
        var dangerHover = CreateBox(Magenta.HoveredElement, Magenta.Text, 6);
        var dangerPressed = CreateBox(Magenta.PressedElement, Magenta.Base, 6);
        var barBackground = CreateBox(Cyan.BackgroundDark, Cyan.Element, 0);
        var memoryFill = new StyleBoxFlat(Cyan.Base);
        var traceFill = new StyleBoxFlat(Magenta.Base);
        var journalFont = ResCache.GetFont("/Fonts/RobotoMono/RobotoMono-Bold.ttf", size: 11);

        return
        [
            E<PanelContainer>().Class(Frame).Panel(frame),
            E<PanelContainer>().Class(Panel).Panel(panel),
            E<PanelContainer>().Class(Card).Panel(card),
            E<Label>().Class(Heading).Font(sheet.BaseFont.GetFont(18, FontKind.Bold)).FontColor(Cyan.Base),
            E<Label>().Class(Caption).Font(sheet.BaseFont.GetFont(11)).FontColor(Cyan.TextDark),
            E<Label>().Class(Accent).Font(sheet.BaseFont.GetFont(13, FontKind.Bold)).FontColor(Magenta.Base),
            E<RichTextLabel>().Class(Accent).Font(sheet.BaseFont.GetFont(13, FontKind.Bold)).FontColor(Magenta.Base),
            E<RichTextLabel>().Class(StyleClass.LabelMonospaceText).Font(journalFont),
            E<ContainerButton>().Class(Button).Box(button),
            E<ContainerButton>().Class(Button).PseudoHovered().Box(buttonHover),
            E<ContainerButton>().Class(Button).PseudoPressed().Box(buttonPressed),
            E<ContainerButton>().Class(Button).PseudoDisabled().Box(buttonDisabled),
            E<ContainerButton>().Class(DangerButton).Box(danger),
            E<ContainerButton>().Class(DangerButton).PseudoHovered().Box(dangerHover),
            E<ContainerButton>().Class(DangerButton).PseudoPressed().Box(dangerPressed),
            E<ContainerButton>().Class(DangerButton).PseudoDisabled().Box(buttonDisabled),
            E<LineEdit>().Class(Search).Prop(LineEdit.StylePropertyStyleBox, panel),
            E<LineEdit>().Class(Search).Prop(LineEdit.StylePropertyCursorColor, Cyan.Base),
            E<ProgressBar>().Class(MemoryBar).Prop(ProgressBar.StylePropertyBackground, barBackground)
                .Prop(ProgressBar.StylePropertyForeground, memoryFill),
            E<ProgressBar>().Class(TraceBar).Prop(ProgressBar.StylePropertyBackground, barBackground)
                .Prop(ProgressBar.StylePropertyForeground, traceFill),
            E<ItemList>().Class(CameraList).Prop(ItemList.StylePropertyBackground, panel)
                .Prop(ItemList.StylePropertyItemBackground, new StyleBoxFlat(Cyan.Background))
                .Prop(ItemList.StylePropertySelectedItemBackground, button)
                .Prop(ItemList.StylePropertyDisabledItemBackground, buttonDisabled),
        ];
    }

    private static StyleBoxFlat CreateBox(Color background, Color border, float padding)
    {
        var box = new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = new Thickness(1),
        };
        box.SetContentMarginOverride(StyleBox.Margin.All, padding);
        return box;
    }
}
