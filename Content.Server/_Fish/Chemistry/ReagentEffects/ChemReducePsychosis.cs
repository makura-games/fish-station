using System.Linq;
using Content.Server._Fish.Psychosis;
using Content.Shared._Fish.Psychosis;
using Content.Shared.EntityEffects;
using Robust.Shared.IoC;

namespace Content.Server._Fish.Chemistry.ReagentEffects;

public sealed partial class ChemPsychosisEntityEffectSystem : EntityEffectSystem<PsychosisComponent, ChemPsychosis>
{
    [Dependency] private PsychosisSystem _psychosis = default!;

    protected override void Effect(Entity<PsychosisComponent> entity, ref EntityEffectEvent<ChemPsychosis> args)
    {
        _psychosis.AddSource(entity, args.Effect.SourceId, args.Effect.AmountPerUnit * args.Scale);
    }
}

public sealed partial class ChemReducePsychosisEntityEffectSystem
    : EntityEffectSystem<PsychosisComponent, ChemReducePsychosis>
{
    protected override void Effect(Entity<PsychosisComponent> entity, ref EntityEffectEvent<ChemReducePsychosis> args)
    {
        var amount = args.Effect.ReducePerUnit * args.Scale;

        foreach (var key in entity.Comp.Sources.Keys.ToArray())
        {
            entity.Comp.Sources[key] -= amount;
            if (entity.Comp.Sources[key] <= 0)
                entity.Comp.Sources.Remove(key);
        }

        Dirty(entity);
    }
}