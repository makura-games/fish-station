using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared._Fish.MentalHealth;
using Content.Shared._Fish.Stress;
using Content.Shared._Fish.Neuro;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Chemistry.ReagentEffects;

public abstract partial class MentalEffectsReagentBase<T> : EntityEffectBase<T>
    where T : EntityEffectBase<T>
{
    [DataField]
    public float StressModifier;

    [DataField]
    public float NeuroModifier;
}

public sealed partial class AlcoholMentalEffects : MentalEffectsReagentBase<AlcoholMentalEffects>
{
    public AlcoholMentalEffects()
    {
        StressModifier = -2f;
        NeuroModifier = -0.5f;
    }
}

public sealed class AlcoholMentalEffectsStressSystem
    : EntityEffectSystem<StressComponent, AlcoholMentalEffects>
{
    protected override void Effect(Entity<StressComponent> entity, ref EntityEffectEvent<AlcoholMentalEffects> args)
    {
        entity.Comp.Current = Math.Clamp(
            entity.Comp.Current + args.Effect.StressModifier * args.Scale,
            0f,
            MentalHealthConstants.StressMax);
        Dirty(entity);
    }
}

public sealed class AlcoholMentalEffectsNeuroSystem
    : EntityEffectSystem<NeuroComponent, AlcoholMentalEffects>
{
    protected override void Effect(Entity<NeuroComponent> entity, ref EntityEffectEvent<AlcoholMentalEffects> args)
    {
        entity.Comp.Current = Math.Clamp(
            entity.Comp.Current + args.Effect.NeuroModifier * args.Scale,
            0f,
            entity.Comp.Max - entity.Comp.PermanentLoss);
        Dirty(entity);
    }
}