using Content.Shared.EntityEffects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

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

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString(
            "entity-effect-guidebook-fish-mental-health",
            ("chance", Probability),
            ("stress", StressPerUnit),
            ("neuro", NeuroPerUnit),
            ("addiction", AddictionPerDose));
}
