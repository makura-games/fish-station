using Content.Server._Fish.Psychosis;
using Content.Shared._Fish.Psychosis;
using Content.Shared.EntityEffects;
using Robust.Shared.IoC;

namespace Content.Server._Fish.Chemistry.ReagentEffects;

public sealed partial class ChemPsychosisEntityEffectSystem : EntityEffectSystem<PsychosisComponent, ChemPsychosis>
{
    [Dependency] private PsychosisSystem _psychosis = default!;

    /// <summary>
    /// Добавляет психоз от метаболизируемого источника.
    /// </summary>
    protected override void Effect(Entity<PsychosisComponent> entity, ref EntityEffectEvent<ChemPsychosis> args)
    {
        _psychosis.AddSource(entity, args.Effect.SourceId, args.Effect.AmountPerUnit * args.Scale);
    }
}
