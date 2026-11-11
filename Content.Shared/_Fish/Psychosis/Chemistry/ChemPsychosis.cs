using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Psychosis;

public sealed partial class ChemPsychosis : EntityEffectBase<ChemPsychosis>
{
    [DataField]
    public string SourceId = "Chemical";

    [DataField]
    public float AmountPerUnit = 1f;
}

public sealed partial class ChemReducePsychosis : EntityEffectBase<ChemReducePsychosis>
{
    [DataField]
    public float ReducePerUnit = 1f;
}
