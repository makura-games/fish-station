namespace Content.Shared._Fish.Xenobiology.Extracts;

/// <summary>
/// Прототип участвует в автопуле gold crit (аналог tg gold_core_spawnable).
/// </summary>
public enum FishGoldCoreSpawnTier : byte
{
    Hostile,
    Lesser,
    Friendly,
}

[RegisterComponent]
public sealed partial class FishSlimeExtractGoldCoreSpawnableComponent : Component
{
    [DataField(required: true)]
    public FishGoldCoreSpawnTier Tier;

    /// <summary>Вес в общем капе <see cref="FishSlimeExtractGoldCritPools.MaxActive"/>.</summary>
    [DataField]
    public int CapWeight = 1;
}
