using Content.Shared.EntityEffects;

namespace Content.Shared._Fish.MentalHealth;

public sealed partial class ChemMentalHealth : EntityEffectBase<ChemMentalHealth>
{
    [DataField]
    public string SubstanceId = "Unknown";

    [DataField]
    public float StressPerUnit;

    [DataField]
    public float NeuroPerUnit;

    [DataField]
    public float AddictionPerDose;
}
