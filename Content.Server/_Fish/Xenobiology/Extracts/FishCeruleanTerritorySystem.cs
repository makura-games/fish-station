using Content.Shared._Fish.Xenobiology.Extracts;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Fish.Xenobiology.Extracts;

/// <summary>
/// Cerulean blood: одна реакция territory blueprint на чанк сетки (~21 тайл).
/// </summary>
public sealed class FishCeruleanTerritorySystem : EntitySystem
{
    public const int ChunkSize = 21;

    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <returns>false — в этом чанке уже была territory-реакция.</returns>
    public bool TryClaimTerritoryChunk(EntityUid extract, EntityUid? user)
    {
        if (!TryComp<MapGridComponent>(Transform(extract).GridUid, out var grid))
            return true;

        var gridUid = Transform(extract).GridUid!.Value;
        var local = _transform.GetGridOrMapTilePosition(extract, grid);
        var chunk = new Vector2i(local.X / ChunkSize, local.Y / ChunkSize);

        var tracker = EnsureComp<FishCeruleanTerritoryTrackerComponent>(gridUid);
        if (!tracker.UsedChunks.Add(chunk))
            return false;

        Dirty(gridUid, tracker);
        return true;
    }
}
