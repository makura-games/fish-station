using System.Linq;
using Content.Server._Fish.MentalHealth;
using Content.Shared._Fish.Psychosis;
using Content.Shared.EntityEffects;
using Robust.Shared.IoC;

namespace Content.Server._Fish.Chemistry.ReagentEffects;

public sealed partial class ChemReducePsychosisEntityEffectSystem
    : EntityEffectSystem<PsychosisComponent, ChemReducePsychosis>
{
    [Dependency] private MentalHealthExposureSystem _exposure = default!;

    /// <summary>
    /// Уменьшает интенсивность активных источников психоза с учётом толерантности.
    /// </summary>
    protected override void Effect(Entity<PsychosisComponent> entity, ref EntityEffectEvent<ChemReducePsychosis> args)
    {
        var multiplier = _exposure.RecordDose(
            entity,
            args.Effect.SubstanceId,
            args.Effect.AddictionPerDose);
        var amount = args.Effect.ReducePerUnit * args.Scale * multiplier;
        if (amount <= 0f)
            return;

        foreach (var key in entity.Comp.Sources.Keys.ToArray())
        {
            entity.Comp.Sources[key] -= amount;
            if (entity.Comp.Sources[key] <= 0)
                entity.Comp.Sources.Remove(key);
        }

        Dirty(entity);
    }
}
