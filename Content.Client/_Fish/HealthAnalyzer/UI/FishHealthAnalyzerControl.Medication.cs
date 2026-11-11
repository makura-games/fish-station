using Content.Client.HealthAnalyzer.UI;
using Content.Client.Stylesheets;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Fish.HealthAnalyzer.UI;

public sealed partial class FishHealthAnalyzerControl
{
    // Количества обновляются только из показаний сканера, а не из скрытых растворов пациента.
    private Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> _medicationAmounts = new();

    private void UpdateMedicationAmounts(IReadOnlyList<ReagentQuantity> reagents)
    {
        _medicationAmounts = HealthAnalyzerMedicationSafety.GetAmounts(reagents);
    }

    /// <summary>
    /// Only bloodstream effects apply to the quantities reported by this scanner.
    /// Unknown thresholds remain unknown rather than implying a safe dose.
    /// </summary>
    private static FixedPoint2? GetMedicationThreshold(ReagentPrototype reagent)
    {
        if (reagent.Metabolisms == null ||
            !reagent.Metabolisms.Metabolisms.TryGetValue("Bloodstream", out var metabolism))
            return null;

        return HealthAnalyzerMedicationSafety.FindThreshold(reagent.ID, metabolism.Effects);
    }

    private void DrawMedicationAlerts()
    {
        foreach (var (id, amount) in _medicationAmounts)
        {
            if (!_prototypes.TryIndex(id, out var reagent))
                continue;

            var threshold = GetMedicationThreshold(reagent);
            var risk = HealthAnalyzerMedicationSafety.GetRisk(amount, threshold);
            if (risk == MedicationRisk.None)
                continue;

            var key = risk == MedicationRisk.ThresholdReached
                ? "health-analyzer-window-medication-alert-overdose"
                : "health-analyzer-window-medication-alert-near";
            AlertsListContainer.AddChild(CreateMedicationText(
                Loc.GetString(key, ("reagent", reagent.LocalizedName), ("amount", amount), ("threshold", threshold!.Value)),
                risk == MedicationRisk.ThresholdReached ? StyleClass.StatusCritical : StyleClass.StatusWarning));
            AlertsContainer.Visible = true;
        }
    }

    private RichTextLabel CreateMedicationText(string text, string style = StyleClass.LabelWeak)
    {
        var label = new RichTextLabel
        {
            HorizontalExpand = true,
            StyleClasses = { HealthAnalyzerSheetlet.DamageType },
        };
        var color = !IsScanActive
            ? HealthAnalyzerSheetlet.InactiveTextColor
            : style == StyleClass.LabelWeak ? HealthAnalyzerSheetlet.SecondaryText : GetStatusColor(style);
        label.SetMessage(text, color);
        return label;
    }

    private void AddMedicationSafety(BoxContainer container, ReagentPrototype reagent, FixedPoint2 amount)
    {
        var threshold = GetMedicationThreshold(reagent);
        var risk = HealthAnalyzerMedicationSafety.GetRisk(amount, threshold);
        if (!threshold.HasValue)
        {
            container.AddChild(CreateMedicationText(Loc.GetString("health-analyzer-window-medication-threshold-unknown")));
            return;
        }

        var key = risk switch
        {
            MedicationRisk.ThresholdReached => "health-analyzer-window-medication-overdose",
            MedicationRisk.NearThreshold => "health-analyzer-window-medication-near",
            _ => "health-analyzer-window-medication-threshold",
        };
        var style = risk switch
        {
            MedicationRisk.ThresholdReached => StyleClass.StatusCritical,
            MedicationRisk.NearThreshold => StyleClass.StatusWarning,
            _ => StyleClass.LabelWeak,
        };
        var label = CreateMedicationText(Loc.GetString(key, ("threshold", threshold.Value)), style);
        label.ToolTip = Loc.GetString("health-analyzer-window-medication-threshold-tooltip");
        container.AddChild(label);
    }

    private void AddReagentSafetyRow(BoxContainer row, ReagentPrototype reagent, FixedPoint2 amount)
    {
        var entry = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 2 };
        entry.AddChild(row);
        if (reagent.Group == "Medicine" || GetMedicationThreshold(reagent).HasValue)
            AddMedicationSafety(entry, reagent, amount);
        ReagentsListContainer.AddChild(entry);
    }
}
