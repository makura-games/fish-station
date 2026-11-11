using Content.Shared.EntityEffects;

namespace Content.Shared._Fish.MentalHealth;

public sealed partial class ChemDetox : EntityEffectBase<ChemDetox>
{
    [DataField]
    public float AddictionReductionPerUnit = 5f;
}
