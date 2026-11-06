namespace Content.Shared._Fish.Xenobiology;

/// <summary>The inherited trait affected by a xenobiological gene treatment.</summary>
public enum FishXenobiologyTreatment : byte
{
    Mutation,
    Stability,
    Growth,
    Yield,
    Fertility,
    Docility,
    Thermal,
}

/// <summary>A single-use treatment applied directly to a laboratory slime.</summary>
[RegisterComponent]
public sealed partial class FishXenobiologySerumComponent : Component
{
    /// <summary>The bounded, heritable genetic change attempted by this treatment.</summary>
    [DataField(required: true)]
    public FishXenobiologyTreatment Treatment;

    /// <summary>Whether an application already consumed this treatment.</summary>
    public bool Used;
}
