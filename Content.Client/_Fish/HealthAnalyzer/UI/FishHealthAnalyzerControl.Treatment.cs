using Content.Client.HealthAnalyzer.UI;
using Content.Client.Stylesheets;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.MedicalScanner;
using Content.Shared.Medical.Healing;
using Content.Shared.Mobs;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Fish.HealthAnalyzer.UI;

public sealed partial class FishHealthAnalyzerControl
{
    /* Формирует подсказки по лечению для данных, уже полученных анализатором. */
    private const float LowBloodLevel = 0.85f;
    private const float CryogenicMedicineMaxTemperature = 213f;
    private static readonly FixedPoint2 MinorDamageThreshold = 10;
    private static readonly FixedPoint2 SevereDamageThreshold = 30;
    private static readonly ProtoId<DamageGroupPrototype> BruteDamageGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnDamageGroup = "Burn";
    private static readonly ProtoId<DamageTypePrototype> CellularDamage = "Cellular";
    private static readonly ProtoId<DamageTypePrototype> ManglenessDamage = "Mangleness";
    private static readonly EntProtoId[] MinorInjuryTreatments = ["Gauze", "Brutepack", "Ointment"];
    private static readonly HashSet<ProtoId<ReagentPrototype>> RazoriumReactants =
    [
        "Bicaridine",
        "Lacerinol",
        "Bruizine",
        "Puncturase",
    ];

    private static readonly Dictionary<ProtoId<DamageGroupPrototype>, ProtoId<ReagentPrototype>> BasicTreatments = new()
    {
        ["Brute"] = "Bicaridine",
        ["Burn"] = "Kelotane",
        ["Airloss"] = "Dexalin",
        ["Toxin"] = "Dylovene",
        ["Genetic"] = "Doxarubixadone",
    };

    private static readonly Dictionary<ProtoId<DamageTypePrototype>, ProtoId<ReagentPrototype>> BasicTypeTreatments = new()
    {
        ["Radiation"] = "Hyronalin",
    };

    private static readonly Dictionary<ProtoId<DamageTypePrototype>, ProtoId<ReagentPrototype>> AdvancedTreatments = new()
    {
        ["Blunt"] = "Bruizine",
        ["Slash"] = "Lacerinol",
        ["Piercing"] = "Puncturase",
        ["Heat"] = "Pyrazine",
        ["Shock"] = "Insuzine",
        ["Cold"] = "Leporazine",
        ["Caustic"] = "Sigynate",
        ["Asphyxiation"] = "DexalinPlus",
        ["Bloodloss"] = "DexalinPlus",
        ["Poison"] = "Diphenhydramine",
        ["Radiation"] = "Arithrazine",
        ["Cellular"] = "Doxarubixadone",
    };

    private void DrawTreatmentAssistant(
        EntityUid target,
        MobState mobState,
        FixedPoint2 totalDamage,
        IReadOnlyDictionary<ProtoId<DamageGroupPrototype>, FixedPoint2> damageGroups,
        IReadOnlyDictionary<ProtoId<DamageTypePrototype>, FixedPoint2> damageTypes,
        HealthAnalyzerUiState state)
    {
        TreatmentListContainer.RemoveAllChildren();

        if (float.IsNaN(state.BloodLevel))
        {
            AddTreatmentText("health-analyzer-window-treatment-unsupported");
            return;
        }

        if (mobState == MobState.Dead)
        {
            DrawDeadTreatment(target, totalDamage, damageGroups, damageTypes, state);
            return;
        }

        var recommendations = new List<TreatmentRecommendation>();
        var addedReagents = new HashSet<ProtoId<ReagentPrototype>>();

        var coveredDamage = new HashSet<ProtoId<DamageTypePrototype>>();
        var bleedingCovered = false;
        if (mobState == MobState.Alive && totalDamage <= MinorDamageThreshold && state.BloodLevel >= LowBloodLevel)
            bleedingCovered = DrawMinorInjuryTreatments(target, damageTypes, state.Bleeding == true, coveredDamage);

        if (mobState == MobState.Critical)
        {
            AddRecommendation(
                recommendations,
                addedReagents,
                "Epinephrine",
                Loc.GetString("health-analyzer-window-treatment-critical"));
        }

        if (state.Bleeding == true && !bleedingCovered)
        {
            AddRecommendation(
                recommendations,
                addedReagents,
                "TranexamicAcid",
                Loc.GetString("health-analyzer-window-treatment-bleeding"));
        }

        if (state.BloodLevel < LowBloodLevel)
        {
            AddRecommendation(
                recommendations,
                addedReagents,
                "Saline",
                Loc.GetString(
                    "health-analyzer-window-treatment-low-blood",
                    ("amount", (state.BloodLevel * 100f).ToString("F1"))));
        }

        foreach (var (damageGroup, damageAmount) in damageGroups)
        {
            foreach (var (damageType, reagent) in GetDamageTreatments(
                         damageGroup,
                         damageAmount,
                         _prototypes.Index(damageGroup).DamageTypes,
                         damageTypes,
                         coveredDamage))
            {
                var condition = Loc.GetString(
                    "health-analyzer-window-treatment-damage",
                    ("damage", _prototypes.Index(damageType).LocalizedName),
                    ("amount", damageTypes[damageType]));
                AddRecommendation(recommendations, addedReagents, reagent, condition);
            }
        }

        if (HasRazoriumInteraction(addedReagents, _medicationAmounts))
        {
            TreatmentListContainer.AddChild(CreateMedicationText(
                Loc.GetString("health-analyzer-window-treatment-razorium-warning"),
                StyleClass.StatusCritical));
        }

        if (recommendations.Count == 0)
        {
            if (TreatmentListContainer.ChildCount == 0)
                AddTreatmentText("health-analyzer-window-treatment-none");
            return;
        }

        foreach (var recommendation in recommendations)
        {
            DrawTreatmentRecommendation(recommendation);
        }

        if (addedReagents.Contains("Doxarubixadone") &&
            (float.IsNaN(state.Temperature) || state.Temperature > CryogenicMedicineMaxTemperature))
        {
            AddTreatmentText(
                "health-analyzer-window-treatment-cryo-required",
                "LabelSubText",
                ("temperature", (CryogenicMedicineMaxTemperature - Atmospherics.T0C).ToString("F1")));
        }

        AddTreatmentText("health-analyzer-window-treatment-warning", "LabelSubText");
    }

    /// <summary>
    /// Returns true when following the recommendations would combine two brute medicines
    /// whose reaction produces razorium.
    /// </summary>
    internal static bool HasRazoriumInteraction(
        IReadOnlySet<ProtoId<ReagentPrototype>> recommendations,
        IReadOnlyDictionary<ProtoId<ReagentPrototype>, FixedPoint2> activeReagents)
    {
        ProtoId<ReagentPrototype>? firstReactant = null;

        foreach (var (reagent, amount) in activeReagents)
        {
            if (amount <= 0 || !RazoriumReactants.Contains(reagent))
                continue;

            if (firstReactant is { } first && first != reagent)
                return true;

            firstReactant = reagent;
        }

        foreach (var reagent in recommendations)
        {
            if (!RazoriumReactants.Contains(reagent))
                continue;

            if (firstReactant is { } first && first != reagent)
                return true;

            firstReactant = reagent;
        }

        return false;
    }

    /// <summary>
    /// Uses the item's healing prototype to avoid recommending dressings for unsupported damage containers.
    /// Covered damage types are excluded from subsequent medication recommendations.
    /// </summary>
    private bool DrawMinorInjuryTreatments(
        EntityUid target,
        IReadOnlyDictionary<ProtoId<DamageTypePrototype>, FixedPoint2> damageTypes,
        bool bleeding,
        HashSet<ProtoId<DamageTypePrototype>> coveredDamage)
    {
        if (!_entityManager.TryGetComponent<DamageableComponent>(target, out var damageable))
            return false;

        var bleedingCovered = false;
        foreach (var item in MinorInjuryTreatments)
        {
            if (!_prototypes.TryIndex(item, out var prototype) ||
                !prototype.TryGetComponent<HealingComponent>(out var healing, _entityManager.ComponentFactory))
                continue;

            if (healing.DamageContainers != null && damageable.DamageContainerID is { } container &&
                !healing.DamageContainers.Contains(container))
                continue;

            var conditions = new List<string>();
            foreach (var (type, amount) in healing.Damage.DamageDict)
            {
                var damage = damageTypes.GetValueOrDefault(type);
                if (amount >= 0 || damage <= 0 || !coveredDamage.Add(type))
                    continue;

                conditions.Add(Loc.GetString(
                    "health-analyzer-window-treatment-damage",
                    ("damage", _prototypes.Index(type).LocalizedName),
                    ("amount", damage)));
            }

            if (bleeding && !bleedingCovered && healing.BloodlossModifier < 0)
            {
                conditions.Add(Loc.GetString("health-analyzer-window-treatment-bleeding"));
                bleedingCovered = true;
            }

            if (conditions.Count == 0)
                continue;

            var name = new FormattedMessage();
            name.AddText(prototype.Name);
            DrawRecommendation(name, string.Join("\n", conditions));
        }

        return bleedingCovered;
    }

    /// <summary>
    /// Uses a separate treatment path because ordinary medicines may not metabolize after death.
    /// Reports the patient's revival threshold without promising that medication alone can revive them.
    /// </summary>
    private void DrawDeadTreatment(
        EntityUid target,
        FixedPoint2 totalDamage,
        IReadOnlyDictionary<ProtoId<DamageGroupPrototype>, FixedPoint2> damageGroups,
        IReadOnlyDictionary<ProtoId<DamageTypePrototype>, FixedPoint2> damageTypes,
        HealthAnalyzerUiState state)
    {
        if (state.Unrevivable == true)
        {
            AddTreatmentText("health-analyzer-window-treatment-dead-unrevivable");
            return;
        }

        if (_mobThresholds.TryGetDeadThreshold(target, out var deadThreshold))
        {
            var requiredHealing = FixedPoint2.Max(FixedPoint2.Zero, totalDamage - deadThreshold.Value);
            AddTreatmentText(
                "health-analyzer-window-treatment-dead-target",
                null,
                ("threshold", deadThreshold.Value),
                ("amount", requiredHealing));
        }

        if (float.IsNaN(state.Temperature) || state.Temperature > CryogenicMedicineMaxTemperature)
        {
            AddTreatmentText(
                "health-analyzer-window-treatment-cryo-required",
                "LabelSubText",
                ("temperature", (CryogenicMedicineMaxTemperature - Atmospherics.T0C).ToString("F1")));
        }

        var recommendations = new List<TreatmentRecommendation>();
        var addedReagents = new HashSet<ProtoId<ReagentPrototype>>();
        var bruteDamage = damageGroups.GetValueOrDefault(BruteDamageGroup);
        var burnDamage = damageGroups.GetValueOrDefault(BurnDamageGroup);

        if (bruteDamage > 0 && burnDamage > 0)
        {
            AddRecommendation(
                recommendations,
                addedReagents,
                "Arcryox",
                Loc.GetString("health-analyzer-window-treatment-dead-brute-burn"));
        }
        else if (bruteDamage > 0)
        {
            AddRecommendation(
                recommendations,
                addedReagents,
                "Brutedon",
                _prototypes.Index(BruteDamageGroup).LocalizedName);
        }
        else if (burnDamage > 0)
        {
            AddRecommendation(
                recommendations,
                addedReagents,
                "Arcryox",
                _prototypes.Index(BurnDamageGroup).LocalizedName);
        }

        AddDeadDamageTreatment(recommendations, addedReagents, damageTypes, "Radiation", "H-32");

        foreach (var recommendation in recommendations)
        {
            DrawTreatmentRecommendation(recommendation);
        }

        // Антидон и целлиминол требуют несинтезируемых ингредиентов; обычные аналоги не работают на мёртвом.
        AddSpecializedTreatmentNotice(damageTypes, "Poison");
        AddSpecializedTreatmentNotice(damageTypes, CellularDamage);
        AddSpecializedTreatmentNotice(damageTypes, ManglenessDamage);

        AddTreatmentText("health-analyzer-window-treatment-dead-next");
        AddTreatmentText("health-analyzer-window-treatment-warning", "LabelSubText");
    }

    private void AddDeadDamageTreatment(
        List<TreatmentRecommendation> recommendations,
        HashSet<ProtoId<ReagentPrototype>> addedReagents,
        IReadOnlyDictionary<ProtoId<DamageTypePrototype>, FixedPoint2> damageTypes,
        ProtoId<DamageTypePrototype> damageType,
        ProtoId<ReagentPrototype> reagent)
    {
        if (damageTypes.GetValueOrDefault(damageType) <= 0)
            return;

        AddRecommendation(
            recommendations,
            addedReagents,
            reagent,
            _prototypes.Index<DamageTypePrototype>(damageType).LocalizedName);
    }

    private void AddSpecializedTreatmentNotice(
        IReadOnlyDictionary<ProtoId<DamageTypePrototype>, FixedPoint2> damageTypes,
        ProtoId<DamageTypePrototype> damageType)
    {
        if (damageTypes.GetValueOrDefault(damageType) <= 0)
            return;

        AddTreatmentText(
            "health-analyzer-window-treatment-dead-specialized",
            "LabelSubText",
            ("damage", _prototypes.Index(damageType).LocalizedName));
    }

    /// <summary>
    /// Selects a treatment for each uncovered damage type. Escalation uses total damage in the group,
    /// not the largest individual injury; multiple reasons for the same medicine are merged by the caller.
    /// </summary>
    internal static IEnumerable<(ProtoId<DamageTypePrototype> DamageType, ProtoId<ReagentPrototype> Reagent)> GetDamageTreatments(
        ProtoId<DamageGroupPrototype> damageGroup,
        FixedPoint2 groupDamage,
        IEnumerable<ProtoId<DamageTypePrototype>> groupTypes,
        IReadOnlyDictionary<ProtoId<DamageTypePrototype>, FixedPoint2> damageTypes,
        IReadOnlySet<ProtoId<DamageTypePrototype>> coveredDamage)
    {
        if (groupDamage <= 0 || !BasicTreatments.TryGetValue(damageGroup, out var basicReagent))
            yield break;

        // Учитываем каждый повреждённый тип, а не только наибольший урон в группе.
        foreach (var damageType in groupTypes)
        {
            if (damageTypes.GetValueOrDefault(damageType) <= 0 || coveredDamage.Contains(damageType))
                continue;

            var reagent = groupDamage >= SevereDamageThreshold &&
                          AdvancedTreatments.TryGetValue(damageType, out var advancedReagent)
                ? advancedReagent
                : BasicTypeTreatments.GetValueOrDefault(damageType, basicReagent);
            yield return (damageType, reagent);
        }
    }

    internal static void AddRecommendation(
        List<TreatmentRecommendation> recommendations,
        HashSet<ProtoId<ReagentPrototype>> addedReagents,
        ProtoId<ReagentPrototype> reagent,
        string condition)
    {
        if (addedReagents.Add(reagent))
        {
            recommendations.Add(new TreatmentRecommendation(reagent, condition));
            return;
        }

        for (var i = 0; i < recommendations.Count; i++)
        {
            var recommendation = recommendations[i];
            if (recommendation.Reagent != reagent)
                continue;

            recommendations[i] = recommendation with { Condition = recommendation.Condition + "\n" + condition };
            return;
        }
    }

    private void DrawTreatmentRecommendation(TreatmentRecommendation recommendation)
    {
        if (!_prototypes.TryIndex<ReagentPrototype>(recommendation.Reagent, out var prototype))
            return;

        var activeAmount = _medicationAmounts.GetValueOrDefault(recommendation.Reagent);

        var message = new FormattedMessage();
        message.PushColor(prototype.SubstanceColor);
        message.AddText("● ");
        message.Pop();
        message.AddText(prototype.LocalizedName);
        DrawRecommendation(message, recommendation.Condition, activeAmount, prototype);
    }

    private void DrawRecommendation(
        FormattedMessage name,
        string condition,
        FixedPoint2 activeAmount = default,
        ReagentPrototype? reagent = null)
    {
        var panel = new PanelContainer
        {
            StyleClasses = { HealthAnalyzerSheetlet.Recommendation },
        };
        var body = new BoxContainer
        {
            Margin = new Thickness(7, 5),
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 12,
        };
        var medicine = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            MinWidth = 125,
            MaxWidth = 145,
        };

        var reagentLabel = new RichTextLabel
        {
            HorizontalExpand = true,
        };
        reagentLabel.SetMessage(name);
        medicine.AddChild(reagentLabel);

        if (activeAmount > 0)
        {
            medicine.AddChild(new Label
            {
                Text = Loc.GetString(
                    "health-analyzer-window-treatment-active-amount",
                    ("amount", activeAmount)),
                StyleClasses =
                {
                    HealthAnalyzerSheetlet.ReagentAmount,
                },
            });
        }

        body.AddChild(medicine);
        var details = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 3,
        };
        details.AddChild(CreateMedicationText(condition));
        if (activeAmount > 0)
            details.AddChild(CreateMedicationText(Loc.GetString("health-analyzer-window-medication-present")));
        if (reagent != null)
            AddMedicationSafety(details, reagent, activeAmount);
        body.AddChild(details);
        panel.AddChild(body);
        TreatmentListContainer.AddChild(panel);
    }

    private void AddTreatmentText(string locId, string? styleClass = null, params (string, object)[] args)
    {
        var label = new RichTextLabel
        {
            Text = Loc.GetString(locId, args),
            HorizontalExpand = true,
        };

        if (styleClass != null)
            label.StyleClasses.Add(styleClass);

        TreatmentListContainer.AddChild(label);
    }

    internal readonly record struct TreatmentRecommendation(
        ProtoId<ReagentPrototype> Reagent,
        string Condition);
}
