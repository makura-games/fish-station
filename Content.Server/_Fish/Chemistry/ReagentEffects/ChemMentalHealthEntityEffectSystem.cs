using Content.Server._Fish.MentalHealth;
using Content.Shared._Fish.MentalHealth;
using Content.Shared._Fish.Neuro;
using Content.Shared._Fish.Stress;
using Content.Shared.EntityEffects;
using Robust.Shared.IoC;
using Robust.Shared.Player;

namespace Content.Server._Fish.Chemistry.ReagentEffects;

public sealed partial class ChemMentalHealthEntityEffectSystem
    : EntityEffectSystem<StressComponent, ChemMentalHealth>
{
    [Dependency] private MentalHealthExposureSystem _exposure = default!;

    /// <summary>
    /// Применяет влияние дозы препарата на стресс, нейроны и толерантность.
    /// </summary>
    protected override void Effect(Entity<StressComponent> entity, ref EntityEffectEvent<ChemMentalHealth> args)
    {
        var effect = args.Effect;
        var multiplier = _exposure.RecordDose(entity, effect.SubstanceId, effect.AddictionPerDose);
        var wasHigh = entity.Comp.Current > MentalHealthConstants.StressThresholdHigh;

        entity.Comp.Current = Math.Clamp(
            entity.Comp.Current + effect.StressPerUnit * args.Scale * multiplier,
            0f,
            MentalHealthConstants.StressMax);
        var isHigh = entity.Comp.Current > MentalHealthConstants.StressThresholdHigh;
        if (wasHigh != isHigh)
            RaiseNetworkEvent(new StressEffectsEvent(GetNetEntity(entity), isHigh), Filter.Entities(entity));

        Dirty(entity);

        if (!TryComp<NeuroComponent>(entity, out var neuro))
            return;

        neuro.Current = Math.Clamp(
            neuro.Current + effect.NeuroPerUnit * args.Scale * multiplier,
            0f,
            neuro.Max - neuro.PermanentLoss);
        Dirty(entity.Owner, neuro);
    }
}
