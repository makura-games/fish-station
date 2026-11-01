using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Xenobiology.Extracts;

/// <summary>
/// Зелье ксенобиологии SS220: тип задаёт эффект на слайме.
/// </summary>
public enum FishSlimePotionKind : byte
{
    Stabilizer,
    Mutator,
    Steroid,
    Docility,
    Enhancer,
    Speed,
    Fireproof,
    OilSlick,
    Sentience,
    Transference,
}

[RegisterComponent, NetworkedComponent]
public sealed partial class FishSlimePotionComponent : Component
{
    [DataField(required: true)]
    public FishSlimePotionKind Kind;
}
