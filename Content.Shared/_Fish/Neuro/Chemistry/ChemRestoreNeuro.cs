using Content.Shared.EntityEffects;

namespace Content.Shared._Fish.Neuro;

public sealed partial class ChemRestoreNeuro : EntityEffectBase<ChemRestoreNeuro>
{
    [DataField]
    public string SourceId = "Nootropil";

    [DataField]
    public float RestorePerUnit = 1f;

    [DataField]
    public float AddictionPerDose = 8f;
}
