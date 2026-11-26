using Content.Shared._Fish.Netrunner;
using Content.Shared.Station;
using Content.Shared.SurveillanceCamera.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;

#pragma warning disable IDE0130 // Fish-edit: Расширяем расчёт видимости ЦИИ из папки Fish.
namespace Content.Shared.Silicons.StationAi;

public sealed partial class StationAiVisionSystem
{
    // Fish-edit: Используем штатные тени ЦИИ, но источниками допускаем только камеры нужной станции и собственное кресло.
    [Dependency] private SharedStationSystem _netrunnerStation = default!;
    [Dependency] private EntityQuery<SurveillanceCameraComponent> _netrunnerCameraQuery = default!;
    [Dependency] private EntityQuery<NetrunnerChairComponent> _netrunnerChairQuery = default!;
    [Dependency] private EntityQuery<NetrunnerDisruptionComponent> _netrunnerDisruptionQuery = default!;

    /// <summary>Checks camera coverage using the same occlusion algorithm as the station AI overlay.</summary>
    public bool IsNetrunnerAccessible(Entity<BroadphaseComponent, MapGridComponent> grid,
        Vector2i tile, EntityUid station, EntityUid? chair = null)
    {
        lock (this)
        {
            _singleTiles.Clear();
            var bounds = _lookup.GetLocalBounds(tile, grid.Comp2.TileSize);
            ComputeNetrunnerView(grid, bounds, bounds.Enlarged(8.5f), station, chair, _singleTiles);
            return _singleTiles.Contains(tile);
        }
    }

    /// <summary>Fills viewport tiles visible to active cameras on one authorized station.</summary>
    public void GetNetrunnerView(Entity<BroadphaseComponent, MapGridComponent> grid,
        Box2Rotated worldBounds, EntityUid station, HashSet<Vector2i> visibleTiles, EntityUid? chair = null)
    {
        lock (this)
        {
            visibleTiles.Clear();
            var inverse = _xforms.GetInvWorldMatrix(grid);
            ComputeNetrunnerView(grid, inverse.TransformBox(worldBounds),
                inverse.TransformBox(worldBounds.Enlarged(8.5f)), station, chair, visibleTiles);
        }
    }

    private void ComputeNetrunnerView(Entity<BroadphaseComponent, MapGridComponent> grid,
        Box2 viewportBounds, Box2 expandedBounds, EntityUid station, EntityUid? chair, HashSet<Vector2i> visibleTiles)
    {
        _viewportTiles.Clear();
        _opaque.Clear();
        _seeds.Clear();
        _job.Data.Clear();
        FastPath = false;
        _seedJob.Grid = (grid.Owner, grid.Comp2);
        _seedJob.ExpandedBounds = expandedBounds;
        _parallel.ProcessNow(_seedJob);
        foreach (var seed in _seeds)
        {
            var isOwnChair = chair != null && seed.Owner == chair.Value && _netrunnerChairQuery.HasComp(seed);
            var isActiveCamera = _netrunnerCameraQuery.TryComp(seed, out var camera)
                && camera.Active && !_netrunnerDisruptionQuery.HasComp(seed)
                && _netrunnerStation.GetOwningStation(seed) == station;
            if (!seed.Comp.Enabled || !isOwnChair && !isActiveCamera
                || !_power.IsPowered((seed.Owner, null))
                || seed.Comp.NeedsAnchoring && !Transform(seed).Anchored)
                continue;
            _job.Data.Add(seed);
        }
        if (_job.Data.Count == 0)
            return;
        var tiles = _maps.GetLocalTilesIntersecting(grid, grid, viewportBounds, ignoreEmpty: false);
        while (tiles.MoveNext(out var tile))
            _viewportTiles.Add(tile.GridIndices);
        tiles = _maps.GetLocalTilesIntersecting(grid, grid, expandedBounds, ignoreEmpty: false);
        while (tiles.MoveNext(out var tile))
        {
            if (IsOccluded(grid, tile.GridIndices))
                _opaque.Add(tile.GridIndices);
        }
        for (var i = _job.Vis1.Count; i < _job.Data.Count; i++)
        {
            _job.Vis1.Add(new Dictionary<Vector2i, int>());
            _job.Vis2.Add(new Dictionary<Vector2i, int>());
            _job.SeedTiles.Add(new HashSet<Vector2i>());
            _job.BoundaryTiles.Add(new HashSet<Vector2i>());
        }
        _job.Grid = (grid.Owner, grid.Comp2);
        _job.VisibleTiles = visibleTiles;
        _parallel.ProcessNow(_job, _job.Data.Count);
    }
}
