namespace Content.Shared._Fish.Xenobiology.Extracts;

/// <summary>
/// Моб от gold plasma crit; при смерти освобождает слот в общем капе.
/// </summary>
[RegisterComponent]
public sealed partial class FishSlimeExtractGoldCritComponent : Component
{
    [DataField]
    public int CapWeight = 1;
}
