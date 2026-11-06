namespace Content.Shared._Fish.Xenobiology;

/// <summary>A printed, single-use research gate for industrial slime induction.</summary>
[RegisterComponent]
public sealed partial class FishXenobiologyIndustrialCatalystComponent : Component
{
    /// <summary>Claims the catalyst before any component or deletion callbacks.</summary>
    public bool Used;
}
