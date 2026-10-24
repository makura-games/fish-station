using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Fish.GridWelding;

public sealed partial class GridWeldingSystem
{
    // Выбор части по клику и проверка досягаемости поверхности тайла.
    private bool CanSelectTarget(Entity<GridWeldingComponent> welder, EntityUid user, EntityUid? target)
    {
        if (target == null || HasComp<MapGridComponent>(target))
            return true;

        // Обычные взаимодействия с предметами сохраняются, пока не выбран край на другом гриде.
        return welder.Comp.User == user && HasComp<MapGridComponent>(welder.Comp.Grid) &&
               _transform.GetGrid(target.Value) is { } targetGrid && targetGrid != welder.Comp.Grid;
    }

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
            _map.FindGridsIntersecting(world.MapId, new Box2(world.Position - radius, world.Position + radius),
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

        if (_map.TryFindGridAt(world, out var uid, out var component))
        {
            grid = (uid, component);
            return true;
        }

        grid = default;
        return false;
    }

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
