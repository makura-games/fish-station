using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityConditions.Conditions;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Body;
using Content.Shared.EntityEffects.Effects.Damage;
using Content.Shared.EntityEffects.Effects.StatusEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Client._Fish.HealthAnalyzer;

/// <summary>
/// Reads dose-dependent hazards for display without executing metabolism effects.
/// Unknown or patient-dependent conditions are not treated as universal overdose thresholds.
/// </summary>
public static class HealthAnalyzerMedicationSafety
{
    /// <summary>
    /// Sums all reagent variants by prototype, matching ReagentCondition quantity checks.
    /// </summary>
    public static Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> GetAmounts(IReadOnlyList<ReagentQuantity> reagents)
    {
        var amounts = new Dictionary<ProtoId<ReagentPrototype>, FixedPoint2>();
        foreach (var reagent in reagents)
        {
            if (reagent.Quantity <= 0)
                continue;

            ProtoId<ReagentPrototype> id = reagent.Reagent.Prototype;
            amounts[id] = amounts.GetValueOrDefault(id) + reagent.Quantity;
        }

        return amounts;
    }

    /// <summary>
    /// Finds the first recognized harmful effect gated only by this reagent's minimum amount.
    /// A null result means unknown, not safe at any dose.
    /// </summary>
    public static FixedPoint2? FindThreshold(ProtoId<ReagentPrototype> reagent, IEnumerable<EntityEffect> effects)
    {
        FixedPoint2? threshold = null;
        foreach (var effect in effects)
        {
            if (effect.Probability <= 0 || effect.Conditions is not { Length: > 0 } || !IsHarmful(effect))
                continue;

            var minimum = FixedPoint2.Zero;
            var supported = true;
            foreach (var condition in effect.Conditions)
            {
                // Смешанные, вложенные и ограниченные сверху условия не задают универсального порога.
                if (condition is not ReagentCondition dose || dose.Reagent != reagent || dose.Max != FixedPoint2.MaxValue)
                {
                    supported = false;
                    break;
                }

                minimum = FixedPoint2.Max(minimum, dose.Min);
            }

            if (supported && minimum > 0 && (!threshold.HasValue || minimum < threshold.Value))
                threshold = minimum;
        }

        return threshold;
    }

    /// <summary>
    /// Classifies the displayed quantity. The near-threshold warning starts at 80 percent.
    /// </summary>
    public static MedicationRisk GetRisk(FixedPoint2 amount, FixedPoint2? threshold)
    {
        if (!threshold.HasValue || threshold.Value <= 0 || amount <= 0)
            return MedicationRisk.None;
        if (amount >= threshold.Value)
            return MedicationRisk.ThresholdReached;
        return amount * 5 >= threshold.Value * 4 ? MedicationRisk.NearThreshold : MedicationRisk.None;
    }

    private static bool IsHarmful(EntityEffect effect)
    {
        return effect switch
        {
            HealthChange health => health.Damage.AnyPositive(),
            Jitter jitter => jitter.Time > 0 && jitter.Amplitude > 0,
            Content.Shared.EntityEffects.Effects.StatusEffects.Drunk drunk => drunk.BoozePower > TimeSpan.Zero,
            Vomit => true,
            _ => false,
        };
    }
}

/// <summary>
/// Dose warnings relative to a recognized prototype threshold, not a diagnosis of actual metabolism.
/// </summary>
public enum MedicationRisk : byte
{
    None,
    NearThreshold,
    ThresholdReached,
}
