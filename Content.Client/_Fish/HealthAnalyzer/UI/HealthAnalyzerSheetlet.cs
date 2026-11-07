using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Content.Client.Stylesheets.Palette;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.HealthAnalyzer.UI;

[CommonSheetlet]
public sealed class HealthAnalyzerSheetlet : Sheetlet<PalettedStylesheet>
{
    public const string Card = "HealthAnalyzerCard";
    public const string Window = "HealthAnalyzerWindow";
    public const string WindowHeader = "HealthAnalyzerWindowHeader";
    public const string WindowTitle = "HealthAnalyzerWindowTitle";
    public const string HeaderCard = "HealthAnalyzerHeaderCard";
    public const string PortraitFrame = "HealthAnalyzerPortraitFrame";
    public const string MetricCard = "HealthAnalyzerMetricCard";
    public const string DamageGroup = "HealthAnalyzerDamageGroup";
    public const string DamageType = "HealthAnalyzerDamageType";
    public const string TreatmentCard = "HealthAnalyzerTreatmentCard";
    public const string Recommendation = "HealthAnalyzerRecommendation";
    public const string AlertCard = "HealthAnalyzerAlertCard";
    public const string EmptyCard = "HealthAnalyzerEmptyCard";
    public const string SectionTitle = "HealthAnalyzerSectionTitle";
    public const string SectionHeading = "HealthAnalyzerSectionHeading";
    public const string DamageTrend = "HealthAnalyzerDamageTrend";
    public const string PatientName = "HealthAnalyzerPatientName";
    public const string MetricKey = "HealthAnalyzerMetricKey";
    public const string MetricValue = "HealthAnalyzerMetricValue";
    public const string Status = "HealthAnalyzerStatus";
    public const string ScanMode = "HealthAnalyzerScanMode";
    public const string ReagentAmount = "HealthAnalyzerReagentAmount";

    public static Color AccentColor => MedicalPalette.Base;
    public static Color InactiveTextColor => InactivePalette.Text;
    public static Color SecondaryText => MedicalPalette.TextDark;

    private static readonly ColorPalette MedicalPalette = ColorPalette.FromHexBase(
        "#4FAAB2",
        lightnessShift: 0.05f,
        chromaShift: 0.006f,
        background: Color.FromHex("#182529"),
        text: Color.FromHex("#A6DDE0"));

    private static readonly ColorPalette CriticalPalette = ColorPalette.FromHexBase(
        "#C19443",
        lightnessShift: 0.045f,
        chromaShift: 0.004f,
        background: Color.FromHex("#292318"),
        text: Color.FromHex("#E5C477"));

    private static readonly ColorPalette DeadPalette = ColorPalette.FromHexBase(
        "#D43D46",
        lightnessShift: 0.05f,
        chromaShift: 0.012f,
        background: Color.FromHex("#2F1014"),
        text: Color.FromHex("#F28A90"));

    private static readonly ColorPalette InactivePalette = ColorPalette.FromHexBase(
        "#687277",
        lightnessShift: 0.04f,
        background: Color.FromHex("#23282A"),
        text: Color.FromHex("#A4ABAE"));

    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var card = CreatePanel(MedicalPalette.BackgroundDark, MedicalPalette.Element, new Thickness(2, 0, 0, 0));
        var header = CreatePanel(MedicalPalette.Background, MedicalPalette.Base, new Thickness(0, 0, 0, 2));
        var portrait = CreatePanel(MedicalPalette.BackgroundDark, MedicalPalette.Element, new Thickness(1));
        var metric = CreatePanel(MedicalPalette.BackgroundDark, MedicalPalette.BackgroundLight, new Thickness(1));
        var damageGroup = CreatePanel(MedicalPalette.Background, MedicalPalette.BackgroundLight, new Thickness(0, 0, 0, 1));
        var treatment = CreatePanel(MedicalPalette.BackgroundDark, MedicalPalette.Base, new Thickness(3, 0, 0, 0));
        var recommendation = CreatePanel(MedicalPalette.Background, MedicalPalette.Element, new Thickness(1, 0, 0, 0));
        var alert = CreatePanel(Palettes.Red.BackgroundDark, Palettes.Amber.Base, new Thickness(3, 0, 0, 0));
        var empty = CreatePanel(MedicalPalette.BackgroundDark, MedicalPalette.BackgroundLight, new Thickness(1));
        var windowHeader = CreatePanel(MedicalPalette.BackgroundLight, MedicalPalette.Base, new Thickness(0, 0, 0, 2));

        return
        [
            E<FancyWindow>()
                .Class(Window)
                .ParentOf(E<PanelContainer>().Class(StyleClass.BackgroundPanel))
                .Modulate(MedicalPalette.Background),
            E<PanelContainer>().Class(WindowHeader).Panel(windowHeader),
            E<PanelContainer>().Class(Card).Panel(card),
            E<PanelContainer>().Class(HeaderCard).Panel(header),
            E<PanelContainer>().Class(PortraitFrame).Panel(portrait),
            E<PanelContainer>().Class(MetricCard).Panel(metric),
            E<PanelContainer>().Class(DamageGroup).Panel(damageGroup),
            E<PanelContainer>().Class(TreatmentCard).Panel(treatment),
            E<PanelContainer>().Class(Recommendation).Panel(recommendation),
            E<PanelContainer>().Class(AlertCard).Panel(alert),
            E<PanelContainer>().Class(EmptyCard).Panel(empty),

            E<CollapsibleHeading>().Class(SectionHeading)
                .Box(CreatePanel(Color.Transparent, Color.Transparent, new Thickness(0))),
            E<CollapsibleHeading>().Class(SectionHeading).PseudoPressed()
                .Box(CreatePanel(Color.Transparent, Color.Transparent, new Thickness(0))),
            E<CollapsibleHeading>().Class(SectionHeading).PseudoHovered()
                .Box(CreatePanel(Color.White.WithAlpha(0.04f), Color.Transparent, new Thickness(0))),
            E<CollapsibleHeading>().Class(SectionHeading).PseudoDisabled()
                .Box(CreatePanel(Color.Transparent, Color.Transparent, new Thickness(0))),
            E<Label>().Class(DamageTrend).Font(sheet.BaseFont.GetFont(11)),

            E<Label>()
                .Class(WindowTitle)
                .Font(sheet.BaseFont.GetFont(14, FontKind.Bold))
                .FontColor(MedicalPalette.Text),
            E<Label>()
                .Class(SectionTitle)
                .Font(sheet.BaseFont.GetFont(13, FontKind.Bold))
                .FontColor(MedicalPalette.Text),
            E<Label>()
                .Class(PatientName)
                .Font(sheet.BaseFont.GetFont(16, FontKind.Bold)),
            E<Label>()
                .Class(MetricKey)
                .Font(sheet.BaseFont.GetFont(10))
                .FontColor(MedicalPalette.TextDark),
            E<Label>()
                .Class(MetricValue)
                .Font(sheet.BaseFont.GetFont(14, FontKind.Bold)),
            E<Label>()
                .Class(Status)
                .Font(sheet.BaseFont.GetFont(12, FontKind.Bold)),
            E<Label>()
                .Class(ScanMode)
                .Font(sheet.BaseFont.GetFont(10, FontKind.Bold)),
            E<Label>()
                .Class(DamageType)
                .Font(sheet.BaseFont.GetFont(11))
                .FontColor(MedicalPalette.TextDark),
            E<RichTextLabel>()
                .Class(DamageType)
                .Font(sheet.BaseFont.GetFont(11)),
            E<Label>()
                .Class(ReagentAmount)
                .Font(sheet.BaseFont.GetFont(11, FontKind.Bold))
                .FontColor(MedicalPalette.Text),
        ];
    }

    // Fish-start - палитры и панели для плавной смены состояния интерфейса
    public static ColorPalette GetPalette(HealthAnalyzerTheme theme)
    {
        return theme switch
        {
            HealthAnalyzerTheme.Critical => CriticalPalette,
            HealthAnalyzerTheme.Dead => DeadPalette,
            HealthAnalyzerTheme.Inactive => InactivePalette,
            _ => MedicalPalette,
        };
    }

    public static StyleBoxFlat? CreateThemedPanel(PanelContainer panel, ColorPalette palette)
    {
        if (panel.HasStyleClass(StyleClass.BackgroundPanel))
            return CreatePanel(palette.Background, palette.Base, new Thickness(0));
        if (panel.HasStyleClass(WindowHeader))
            return CreatePanel(palette.BackgroundLight, palette.Base, new Thickness(0, 0, 0, 2));
        if (panel.HasStyleClass(Card))
            return CreatePanel(palette.BackgroundDark, palette.Element, new Thickness(2, 0, 0, 0));
        if (panel.HasStyleClass(HeaderCard))
            return CreatePanel(palette.Background, palette.Base, new Thickness(0, 0, 0, 2));
        if (panel.HasStyleClass(PortraitFrame))
            return CreatePanel(palette.BackgroundDark, palette.Element, new Thickness(1));
        if (panel.HasStyleClass(MetricCard))
            return CreatePanel(palette.BackgroundDark, palette.BackgroundLight, new Thickness(1));
        if (panel.HasStyleClass(DamageGroup))
            return CreatePanel(palette.Background, palette.BackgroundLight, new Thickness(0, 0, 0, 1));
        if (panel.HasStyleClass(TreatmentCard))
            return CreatePanel(palette.BackgroundDark, palette.Base, new Thickness(3, 0, 0, 0));
        if (panel.HasStyleClass(Recommendation))
            return CreatePanel(palette.Background, palette.Element, new Thickness(1, 0, 0, 0));
        if (panel.HasStyleClass(AlertCard))
            return CreatePanel(palette.BackgroundDark, palette.Base, new Thickness(3, 0, 0, 0));
        if (panel.HasStyleClass(EmptyCard))
            return CreatePanel(palette.BackgroundDark, palette.BackgroundLight, new Thickness(1));

        return null;
    }
    // Fish-end

    private static StyleBoxFlat CreatePanel(Color background, Color border, Thickness thickness)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = thickness,
        };
    }
}

public enum HealthAnalyzerTheme : byte
{
    Active,
    Critical,
    Dead,
    Inactive,
}
