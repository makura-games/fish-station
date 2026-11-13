using Content.Shared.EntityEffects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Neuro;

public sealed partial class ChemRestoreNeuro : EntityEffectBase<ChemRestoreNeuro>
{
    [DataField]
    public string SourceId = "FishNootropil";

    [DataField]
    public float RestorePerUnit = 1f;

    [DataField]
    public float AddictionPerDose = 8f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString(
            "entity-effect-guidebook-fish-restore-neuro",
            ("chance", Probability),
            ("amount", RestorePerUnit));
}
