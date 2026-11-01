using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Xenobiology;

/// <summary>A dormant culture that becomes a juvenile slime when hydrated outside a container.</summary>
[RegisterComponent]
public sealed partial class FishXenobiologyCultureComponent : Component
{
    /// <summary>The breed to hatch.</summary>
    [DataField(required: true)]
    public ProtoId<FishXenobiologyBreedPrototype> Breed;

    /// <summary>The solution accepting hydration water.</summary>
    [DataField]
    public string Solution = "culture";

    /// <summary>Whether this culture has already hatched.</summary>
    public bool Used;
}
