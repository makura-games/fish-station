using System.Numerics;
using Content.Shared.Tools.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Fish.GridWelding;

public sealed partial class GridWeldingSystem
{
    /// <summary>
    /// Applies a validated grid click by clearing, replacing or using the current edge selection.
    /// </summary>
    private bool SelectTile(Entity<GridWeldingComponent> welder, EntityUid user, EntityUid grid, Vector2i tile)
    {
        var selection = welder.Comp;
        if (selection.User == user && selection.Grid == grid && selection.Tile == tile)
        {
            ClearSelection(selection);
            Popup(user, "fish-grid-welding-cleared");
            return true;
        }

        if (!HasValidSelection(selection, user))
        {
            selection.User = user;
            selection.Grid = grid;
            selection.Tile = tile;
            Popup(user, "fish-grid-welding-selected");
            return true;
        }

        if (selection.Grid == grid)
        {
            Popup(user, "fish-grid-welding-same-grid");
            return true;
        }

        return TryStartWelding(welder, user, grid, tile);
    }

    /// <summary>
    /// Validates the tool, target and selected surface without creating or changing the welder's selection.
    /// </summary>
    /// <remarks><paramref name="handled"/> is true when the click belongs to an existing weld or produces feedback.</remarks>
    private bool CanSelectTile(Entity<WelderComponent?> welder, EntityUid user, EntityCoordinates location,
        EntityUid? target, out Entity<MapGridComponent> grid, out Vector2i tile, out bool handled, bool quiet = false)
    {
        grid = default;
        tile = default;
        handled = false;
        if (!CanSelectTarget(target) || !CanUseWelder(welder, user))
            return false;

        TryComp(welder, out GridWeldingComponent? selection);
        if (selection?.DoAfter != null)
        {
            handled = true;
            return false;
        }

        var selected = selection is { } state && state.User == user ? (EntityUid?) state.Grid : null;
        if (!TryGetClickedGrid(location, target, selected, out grid))
            return false;

        tile = _maps.GetTileRef(grid, grid.Comp, location).GridIndices;
        handled = true;
        if (!CanReachTile(user, grid, grid.Comp, tile))
        {
            if (!quiet)
                Popup(user, "fish-grid-welding-unreachable");
            return false;
        }

        if (!HasValidSelection(selection, user) && !IsEdgeTile(grid, grid.Comp, tile))
        {
            if (!quiet)
                Popup(user, "fish-grid-welding-edge-required");
            return false;
        }

        return true;
    }

    private bool CanSelectTarget(EntityUid? target) => target == null || HasComp<MapGridComponent>(target);

    /// <summary>Rejects stale selections when ownership changed, the grid vanished or the selected tile was removed.</summary>
    private bool HasValidSelection(GridWeldingComponent? selection, EntityUid user)
    {
        return selection != null && selection.User == user &&
               TryComp(selection.Grid, out MapGridComponent? grid) &&
               !_maps.GetTileRef(selection.Grid, grid, selection.Tile).Tile.IsEmpty;
    }

    /// <summary>Resolves the clicked tile across overlapping grids, preferring an unselected solid candidate.</summary>
    private bool TryGetClickedGrid(EntityCoordinates location, EntityUid? target, EntityUid? selected,
        out Entity<MapGridComponent> grid)
    {
        if (target is { } entity && TryGetSolidGrid(_transform.GetGrid(entity), location, out grid))
            return true;

        var hasReference = TryGetSolidGrid(location.EntityId, location, out grid);
        if (hasReference && grid.Owner != selected)
            return true;

        var world = _transform.ToMapCoordinates(location);
        if (selected is { Valid: true } first)
        {
            // Клиент тоже ищет грид по мировой точке и при перекрытии может снова прислать первый.
            var candidates = new List<Entity<MapGridComponent>>();
            var radius = new Vector2(0.2f);
            _maps.FindGridsIntersecting(world.MapId, new Box2(world.Position - radius, world.Position + radius),
                ref candidates, approx: true, includeMap: false);
            var bestDistance = float.PositiveInfinity;
            var found = false;
            foreach (var candidate in candidates)
            {
                if (candidate.Owner == first)
                    continue;

                var tile = _maps.GetTileRef(candidate, candidate.Comp, location);
                if (tile.Tile.IsEmpty)
                    continue;

                var center = _transform.ToMapCoordinates(_maps.GridTileToLocal(candidate, candidate.Comp, tile.GridIndices));
                var distance = Vector2.DistanceSquared(center.Position, world.Position);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                grid = candidate;
                found = true;
            }
            if (found)
                return true;
        }

        if (hasReference)
            return true;

        if (_maps.TryFindGridAt(world, out var uid, out var component))
        {
            grid = (uid, component);
            return true;
        }

        grid = default;
        return false;
    }

    /// <summary>Accepts a grid only when it still has a nonempty tile at the click on the same map.</summary>
    private bool TryGetSolidGrid(EntityUid? uid, EntityCoordinates location, out Entity<MapGridComponent> grid)
    {
        grid = default;
        if (uid is not { } entity || TerminatingOrDeleted(entity) ||
            !TryComp(entity, out MapGridComponent? component) ||
            Transform(entity).MapID != _transform.GetMapId(location) ||
            _maps.GetTileRef(entity, component, location).Tile.IsEmpty)
            return false;

        grid = (entity, component);
        return true;
    }

    /// <summary>Measures unobstructed reach to the closest point on the tile rather than requiring its center.</summary>
    private bool CanReachTile(EntityUid user, EntityUid uid, MapGridComponent grid, Vector2i tile)
    {
        var position = _transform.WithEntityId(Transform(user).Coordinates, uid).Position;
        var minimum = (Vector2) tile * grid.TileSize;
        var nearest = Vector2.Clamp(position, minimum, minimum + new Vector2(grid.TileSize));
        // До края тайла можно дотянуться, даже когда его центр уже за обычным радиусом взаимодействия.
        return _interaction.InRangeUnobstructed(user,
            _transform.ToMapCoordinates(new EntityCoordinates(uid, nearest)));
    }
}
