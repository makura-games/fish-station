using Content.Server._Fish.MentalHealth;
using Content.Shared._Fish.MentalHealth;
using Content.Shared._Fish.Neuro;
using Content.Shared._Fish.Stress;
using Content.Shared.EntityEffects;
using Robust.Shared.IoC;

namespace Content.Server._Fish.Chemistry.ReagentEffects;

public sealed class ChemMentalHealthEntityEffectSystem
    : EntityEffectSystem<StressComponent, ChemMentalHealth>
{
    [Dependency] private readonly MentalHealthExposureSystem _exposure = default!;

    protected override void Effect(Entity<StressComponent> entity, ref EntityEffectEvent<ChemMentalHealth> args)
    {
        var effect = args.Effect;
        var multiplier = _exposure.RecordDose(entity, effect.SubstanceId, effect.AddictionPerDose);

        entity.Comp.Current = Math.Clamp(
            entity.Comp.Current + effect.StressPerUnit * args.Scale * multiplier,
            0f,
            MentalHealthConstants.StressMax);
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
