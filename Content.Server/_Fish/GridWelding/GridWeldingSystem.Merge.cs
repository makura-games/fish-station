using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Decals;
using Content.Server.Gravity;
using Content.Server.Station.Systems;
using Content.Shared.Database;
using Content.Shared.Decals;
using Content.Shared.Maps;
using Content.Shared.Station.Components;
using Robust.Server.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Fish.GridWelding;

public sealed partial class GridWeldingSystem
{
    // Движок переносит геометрию, а содержимое, принадлежащее игровым системам, переносится через их API.
    [Dependency] private GridFixtureSystem _gridFixture = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private DecalSystem _decal = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private GravitySystem _gravity = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IGameTiming _timing = default!;

    private void MergeGrids(EntityUid first, EntityUid second, WeldAlignment alignment, EntityUid user)
    {
        if (GetSurvivingGrid(first, second) == second)
        {
            // При восстановлении отколовшейся части сохраняем исходный большой грид и его настройки.
            Matrix3x2.Invert(alignment.Matrix, out var inverse);
            alignment = new WeldAlignment(inverse, -alignment.Rotation);
            (first, second) = (second, first);
        }

        var a = Comp<MapGridComponent>(first);
        var b = Comp<MapGridComponent>(second);
        var bodyA = Comp<PhysicsComponent>(first);
        var bodyB = Comp<PhysicsComponent>(second);
        var xformA = Transform(first);
        var xformB = Transform(second);
        var worldA = _transform.GetWorldMatrix(xformA);
        var donorWorld = alignment.Matrix * worldA;
        var momentum = CaptureMomentum(bodyA, bodyB, worldA, donorWorld);
        var atmos = _atmosphere.CaptureGridWeldingAtmosphere(second, alignment.Matrix);
        var decals = _decal.GetDecalsIntersecting(second, b.LocalAABB.Enlarged(1));
        var history = CaptureTileHistory(second, alignment.Matrix);

        if (!HasComp<StationMemberComponent>(first) && TryComp(second, out StationMemberComponent? station))
            _station.AddGridToStation(station.Station, first);
        if (!HasComp<GridWeldingOriginComponent>(first) && TryComp(second, out GridWeldingOriginComponent? origin))
            EnsureComp<GridWeldingOriginComponent>(first).OriginalGrid = origin.OriginalGrid;

        _adminLog.Add(LogType.Construction, LogImpact.High,
            $"{ToPrettyString(user):player} welded {ToPrettyString(second):grid} into {ToPrettyString(first):grid}");

        // Сначала совмещаем физическое положение: перенос между родителями сохранит скорости предметов.
        _transform.SetWorldPositionRotation(second, donorWorld.Translation,
            _transform.GetWorldRotation(xformA) + alignment.Rotation, xformB);

        // Merge ищет незакреплённые сущности только над тайлами. Переносим также детей над пустыми клетками.
        var loose = new List<EntityUid>();
        var children = xformB.ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (!Transform(child).Anchored)
                loose.Add(child);
        }
        foreach (var child in loose)
        {
            var xform = Transform(child);
            _transform.SetCoordinates(child, xform,
                new EntityCoordinates(first, Vector2.Transform(xform.LocalPosition, alignment.Matrix)),
                xform.LocalRotation + alignment.Rotation);
        }

        _gridFixture.Merge(first, second, alignment.Matrix, a, b, xformA, xformB);
        _atmosphere.RestoreGridWeldingAtmosphere(first, atmos);
        _gravity.RefreshGravity(first);
        RestoreTileHistory(first, history);

        RestoreDecals((first, a), decals, alignment);

        if (bodyA.BodyType == BodyType.Dynamic && momentum.Mass > 0)
        {
            _physics.SetLinearVelocity(first, momentum.Linear / momentum.Mass, body: bodyA);
            var inertia = CenterInertia(bodyA);
            _physics.SetAngularVelocity(first, inertia > 0 ? momentum.Angular / inertia : 0, body: bodyA);
        }
    }

    private void RestoreDecals(Entity<MapGridComponent> grid, HashSet<(uint Index, Decal Decal)> decals,
        WeldAlignment alignment)
    {
        if (decals.Count == 0)
            return;

        // Ищем опору после слияния: у целевого грида до него могли быть только решётки.
        var tiles = _maps.GetAllTilesEnumerator(grid, grid.Comp);
        while (tiles.MoveNext(out var tile))
        {
            if (_turf.IsSpace(tile.Value))
                continue;

            EnsureComp<DecalGridComponent>(grid);
            var support = _maps.GridTileToLocal(grid, grid.Comp, tile.Value.GridIndices);
            foreach (var (_, decal) in decals)
            {
                var position = Vector2.Transform(decal.Coordinates + new Vector2(0.5f), alignment.Matrix) - new Vector2(0.5f);
                // После поворота начало декали может оказаться над пустотой. Проверяем опору отдельно от отрисовки.
                _decal.TryAddDecal(decal.WithCoordinates(position).WithRotation(decal.Angle + alignment.Rotation),
                    support, out _);
            }
            return;
        }
    }

    private static (Vector2 Linear, float Angular, float Mass) CaptureMomentum(
        PhysicsComponent a, PhysicsComponent b, Matrix3x2 worldA, Matrix3x2 worldB)
    {
        var mass = a.Mass + b.Mass;
        if (mass <= 0)
            return default;

        var centerA = Vector2.Transform(a.LocalCenter, worldA);
        var centerB = Vector2.Transform(b.LocalCenter, worldB);
        var center = (a.Mass * centerA + b.Mass * centerB) / mass;
        var angular = CenterInertia(a) * a.AngularVelocity + CenterInertia(b) * b.AngularVelocity +
                      Cross(centerA - center, a.Momentum) + Cross(centerB - center, b.Momentum);
        return (a.Momentum + b.Momentum, angular, mass);
    }

    private static float CenterInertia(PhysicsComponent body)
    {
        return Math.Max(0, body.Inertia - body.FixturesMass * body.LocalCenter.LengthSquared());
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    private Dictionary<Vector2i, List<ProtoId<ContentTileDefinition>>> CaptureTileHistory(
        EntityUid grid, Matrix3x2 matrix)
    {
        var result = new Dictionary<Vector2i, List<ProtoId<ContentTileDefinition>>>();
        if (!TryComp(grid, out TileHistoryComponent? history))
            return result;

        foreach (var chunk in history.ChunkHistory.Values)
        {
            foreach (var (tile, layers) in chunk.History)
                result[TransformTile(tile, matrix)] = new(layers);
        }
        return result;
    }

    private void RestoreTileHistory(EntityUid grid,
        Dictionary<Vector2i, List<ProtoId<ContentTileDefinition>>> snapshot)
    {
        if (snapshot.Count == 0)
            return;

        var history = EnsureComp<TileHistoryComponent>(grid);
        foreach (var (tile, layers) in snapshot)
        {
            var index = SharedMapSystem.GetChunkIndices(tile, TileSystem.ChunkSize);
            if (!history.ChunkHistory.TryGetValue(index, out var chunk))
            {
                chunk = new TileHistoryChunk();
                history.ChunkHistory[index] = chunk;
            }

            chunk.History[tile] = layers;
            chunk.LastModified = _timing.CurTick;
        }
        Dirty(grid, history);
    }
}
