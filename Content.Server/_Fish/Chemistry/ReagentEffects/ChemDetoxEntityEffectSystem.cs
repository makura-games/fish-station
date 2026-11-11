using Content.Server._Fish.MentalHealth;
using Content.Shared._Fish.MentalHealth;
using Content.Shared.EntityEffects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server._Fish.Chemistry.ReagentEffects;

public sealed partial class ChemDetoxEntityEffectSystem : EntityEffectSystem<TransformComponent, ChemDetox>
{
    [Dependency] private MentalHealthExposureSystem _exposure = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<ChemDetox> args)
    {
        _exposure.ReduceAddiction(
            entity,
            args.Effect.AddictionReductionPerUnit * args.Scale);
    }
}
