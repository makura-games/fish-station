namespace Content.Shared._Fish.Xenobiology.Extracts;

/// <summary>
/// На grid: cerulean blood уже помечал «территорию» в этом чанке.
/// </summary>
[RegisterComponent]
public sealed partial class FishCeruleanTerritoryTrackerComponent : Component
{
    /// <summary>Индексы чанков сетки (см. FishCeruleanTerritorySystem.ChunkSize).</summary>
    public HashSet<Vector2i> UsedChunks = new();
}
