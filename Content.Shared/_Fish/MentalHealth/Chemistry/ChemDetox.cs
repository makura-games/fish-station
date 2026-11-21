using Content.Shared.EntityEffects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.MentalHealth;

public sealed partial class ChemDetox : EntityEffectBase<ChemDetox>
{
    [DataField]
    public float AddictionReductionPerUnit = 5f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString(
            "entity-effect-guidebook-fish-detox",
            ("chance", Probability),
            ("amount", AddictionReductionPerUnit));
}
