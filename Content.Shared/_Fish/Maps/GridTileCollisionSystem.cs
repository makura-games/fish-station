using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Fish.Maps;

/// <summary>
/// Transparent tiles (<see cref="ContentTileDefinition.EnableGridCollision"/> = false) stay on the map
/// but do not hard-collide with other grids unless a dense anchored blocker occupies the cell.
/// Uses <see cref="PreventCollideEvent"/> so engine grid fixture generation stays unchanged.
/// </summary>
public sealed partial class GridTileCollisionSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedBroadphaseSystem _broadphase = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;

    /// <summary>
    /// Full-tile dense blockers: walls, airlocks, windows, etc.
    /// </summary>
    private const CollisionGroup DenseBlockerMask = CollisionGroup.FullTileMask;

    private readonly HashSet<EntityUid> _gridsWithTransparent = new();
    private readonly HashSet<EntityUid> _pendingContactRegen = new();
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();

        SubscribeLocalEvent<MapGridComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<GridInitializeEvent>(OnGridInit);
        SubscribeLocalEvent<GridRemovalEvent>(OnGridRemoval);
        SubscribeLocalEvent<AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<CollisionChangeEvent>(OnCollisionChanged);
    }

    public override void Update(float frameTime)
    {
        if (_pendingContactRegen.Count == 0)
            return;

        foreach (var gridUid in _pendingContactRegen)
        {
            if (!_physicsQuery.TryGetComponent(gridUid, out var body))
                continue;

            _broadphase.RegenerateContacts(gridUid, body);
        }

        _pendingContactRegen.Clear();
    }

    private void OnGridInit(GridInitializeEvent ev)
    {
        RefreshTransparentTracking(ev.EntityUid);
    }

    private void OnGridRemoval(GridRemovalEvent ev)
    {
        _gridsWithTransparent.Remove(ev.EntityUid);
        _pendingContactRegen.Remove(ev.EntityUid);
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        RefreshTransparentTracking(args.Entity);
        if (_gridsWithTransparent.Contains(args.Entity))
            _pendingContactRegen.Add(args.Entity);
    }

    private void OnAnchorChanged(ref AnchorStateChangedEvent args)
    {
        QueueContactRegenAtEntity(args.Entity);
    }

    private void OnCollisionChanged(ref CollisionChangeEvent args)
    {
        QueueContactRegenAtEntity(args.BodyUid);
    }

    private void QueueContactRegenAtEntity(EntityUid uid)
    {
        if (!_xformQuery.TryGetComponent(uid, out var xform))
            return;

        if (xform.GridUid is not { } gridUid || gridUid == uid)
            return;

        if (!_gridsWithTransparent.Contains(gridUid))
            return;

        if (!TryComp(gridUid, out MapGridComponent? grid))
            return;

        var tile = _map.GetTileRef(gridUid, grid, xform.Coordinates);
        if (tile.Tile.IsEmpty || !IsTransparent(tile.Tile))
            return;

        _pendingContactRegen.Add(gridUid);
    }

    private void RefreshTransparentTracking(EntityUid gridUid)
    {
        if (GridHasTransparentTile(gridUid))
            _gridsWithTransparent.Add(gridUid);
        else
            _gridsWithTransparent.Remove(gridUid);
    }

    private void OnPreventCollide(Entity<MapGridComponent> ent, ref PreventCollideEvent ev)
    {
        if (ev.Cancelled)
            return;

        if (!ev.OurFixture.Hard || !ev.OtherFixture.Hard)
            return;

        var mapGridGroup = MapGridHelpers.CollisionGroup;
        if ((ev.OurFixture.CollisionLayer & mapGridGroup) == 0 ||
            (ev.OtherFixture.CollisionLayer & mapGridGroup) == 0)
            return;

        // Directed event: OurEntity is this grid; other must also be a grid.
        if (!_gridQuery.HasComponent(ev.OtherEntity))
            return;

        if (!_gridsWithTransparent.Contains(ev.OurEntity) &&
            !_gridsWithTransparent.Contains(ev.OtherEntity))
            return;

        if (!_xformQuery.TryGetComponent(ev.OurEntity, out var ourXform) ||
            !_xformQuery.TryGetComponent(ev.OtherEntity, out var otherXform))
            return;

        var ourAabb = FixtureWorldAabb(ev.OurFixture, ourXform);
        var otherAabb = FixtureWorldAabb(ev.OtherFixture, otherXform);

        if (!ourAabb.Intersects(otherAabb))
            return;

        var overlap = ourAabb.Intersect(otherAabb);

        if (OverlapIsPassable(ev.OurEntity, overlap) || OverlapIsPassable(ev.OtherEntity, overlap))
            ev.Cancelled = true;
    }

    private Box2 FixtureWorldAabb(Fixture fixture, TransformComponent xform)
    {
        var (worldPos, worldRot) = _transform.GetWorldPositionRotation(xform);
        var transform = new Transform(worldPos, (float)worldRot.Theta);
        var bounds = fixture.Shape.ComputeAABB(transform, 0);
        for (var i = 1; i < fixture.Shape.ChildCount; i++)
            bounds = bounds.Union(fixture.Shape.ComputeAABB(transform, i));

        return bounds;
    }

    private bool OverlapIsPassable(EntityUid gridUid, Box2 worldOverlap)
    {
        if (!_gridQuery.TryGetComponent(gridUid, out var grid) ||
            !_xformQuery.TryGetComponent(gridUid, out var xform))
            return false;

        var invMatrix = _transform.GetInvWorldMatrix(xform);
        var localBl = Vector2.Transform(worldOverlap.BottomLeft, invMatrix);
        var localBr = Vector2.Transform(worldOverlap.BottomRight, invMatrix);
        var localTl = Vector2.Transform(worldOverlap.TopLeft, invMatrix);
        var localTr = Vector2.Transform(worldOverlap.TopRight, invMatrix);

        var minX = MathF.Min(MathF.Min(localBl.X, localBr.X), MathF.Min(localTl.X, localTr.X));
        var maxX = MathF.Max(MathF.Max(localBl.X, localBr.X), MathF.Max(localTl.X, localTr.X));
        var minY = MathF.Min(MathF.Min(localBl.Y, localBr.Y), MathF.Min(localTl.Y, localTr.Y));
        var maxY = MathF.Max(MathF.Max(localBl.Y, localBr.Y), MathF.Max(localTl.Y, localTr.Y));

        var tileSize = (float)grid.TileSize;
        var tileMin = new Vector2i((int)MathF.Floor(minX / tileSize), (int)MathF.Floor(minY / tileSize));
        var tileMax = new Vector2i((int)MathF.Floor((maxX - 0.001f) / tileSize), (int)MathF.Floor((maxY - 0.001f) / tileSize));

        var foundNonEmpty = false;
        for (var x = tileMin.X; x <= tileMax.X; x++)
        {
            for (var y = tileMin.Y; y <= tileMax.Y; y++)
            {
                var indices = new Vector2i(x, y);
                var tile = _map.GetTileRef(gridUid, grid, indices).Tile;
                if (tile.IsEmpty)
                    continue;

                foundNonEmpty = true;
                if (!IsTilePassableForGrids(gridUid, grid, indices, tile))
                    return false;
            }
        }

        // Проходимо только если в зоне реально есть прозрачные клетки без блокаторов.
        // Пустой overlap (из‑за погрешности AABB) не считаем проходимым.
        return foundNonEmpty;
    }

    private bool IsTilePassableForGrids(EntityUid gridUid, MapGridComponent grid, Vector2i indices, Tile tile)
    {
        if (!IsTransparent(tile))
            return false;

        return !_turf.IsTileBlocked(gridUid, indices, DenseBlockerMask, grid: grid);
    }

    private bool IsTransparent(Tile tile)
    {
        if (tile.IsEmpty)
            return false;

        return _tileDefinitions[tile.TypeId] is ContentTileDefinition def && !def.EnableGridCollision;
    }

    private bool GridHasTransparentTile(EntityUid gridUid)
    {
        if (!_gridQuery.TryGetComponent(gridUid, out var grid))
            return false;

        foreach (var tileRef in _map.GetAllTiles(gridUid, grid))
        {
            if (IsTransparent(tileRef.Tile))
                return true;
        }

        return false;
    }
}
