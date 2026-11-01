using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Xenobiology;

/// <summary>A single-use slime extract that reacts with a measured chemical catalyst.</summary>
[RegisterComponent]
public sealed partial class FishXenobiologyExtractComponent : Component
{
    /// <summary>The breed whose reaction catalog this extract uses.</summary>
    [DataField(required: true)]
    public ProtoId<FishXenobiologyBreedPrototype> Breed;

    /// <summary>The solution accepting injected or poured catalysts.</summary>
    [DataField]
    public string Solution = "extract";

    /// <summary>Whether a reaction has already claimed this extract.</summary>
    public bool Used;
}
