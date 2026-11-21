using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Fish.Research.UI;

[CommonSheetlet]
public sealed class FishResearchSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var selected = Box("#483753", "#C19AE8", 3);

        return
        [
            E<Button>().Class("FishResearchPurchase").PseudoNormal().Box(Box("#493859", "#9573AE")).Modulate(Color.White),
            E<Button>().Class("FishResearchPurchase").PseudoHovered().Box(Box("#604971", "#D3B3ED")).Modulate(Color.White),
            E<Button>().Class("FishResearchPurchase").PseudoPressed().Box(selected).Modulate(Color.White),
            E<Button>().Class("FishResearchPurchase").PseudoDisabled().Box(Box("#303039", "#50505B")).Modulate(Color.White),
            E<Button>().Class("FishResearchLeaf").PseudoNormal().Box(Box("#23232E", "#454250", 2)).Modulate(Color.White),
            E<Button>().Class("FishResearchLeaf").PseudoHovered().Box(Box("#34303F", "#947AA6", 2)).Modulate(Color.White),
            E<Button>().Class("FishResearchLeaf").PseudoPressed().Modulate(Color.White),
            E<Label>().Class("FishResearchDiscipline").Font(sheet.BaseFont.GetFont(13, FontKind.Bold)),
            E<Label>().Class("FishResearchTier").FontColor(Color.FromHex("#BCB2CA")),
            E<RichTextLabel>().Class("FishResearchTitle").Font(sheet.BaseFont.GetFont(14, FontKind.Bold)),
        ];
    }

    internal static StyleBoxFlat SelectedTechnology(Color accent) =>
        Box(Color.InterpolateBetween(Color.FromHex("#23232E"), accent, 0.28f), accent, 3);

    private static StyleBoxFlat Box(string background, string border, int? left = null) =>
        Box(Color.FromHex(background), Color.FromHex(border), left);

    private static StyleBoxFlat Box(Color background, Color border, int? left) => new()
    {
        BackgroundColor = background,
        BorderColor = border,
        BorderThickness = left is { } width ? new Thickness(width, 0, 0, 0) : new Thickness(1),
        ContentMarginLeftOverride = 7,
        ContentMarginRightOverride = 7,
        ContentMarginTopOverride = 3,
        ContentMarginBottomOverride = 3,
    };
}
