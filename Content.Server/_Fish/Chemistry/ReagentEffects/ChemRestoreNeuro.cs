using Content.Server._Fish.MentalHealth;
using Content.Shared._Fish.Neuro;
using Content.Shared.EntityEffects;
using Robust.Shared.IoC;

namespace Content.Server._Fish.Chemistry.ReagentEffects;

public sealed partial class ChemRestoreNeuroEntityEffectSystem
    : EntityEffectSystem<NeuroComponent, ChemRestoreNeuro>
{
    [Dependency] private MentalHealthExposureSystem _exposure = default!;

    protected override void Effect(Entity<NeuroComponent> entity, ref EntityEffectEvent<ChemRestoreNeuro> args)
    {
        var maximum = entity.Comp.Max - entity.Comp.PermanentLoss;
        var multiplier = _exposure.RecordDose(
            entity,
            args.Effect.SourceId,
            args.Effect.AddictionPerDose);
        entity.Comp.Current = Math.Clamp(
            entity.Comp.Current + args.Effect.RestorePerUnit * args.Scale * multiplier,
            0f,
            maximum);
        Dirty(entity);
    }
}
