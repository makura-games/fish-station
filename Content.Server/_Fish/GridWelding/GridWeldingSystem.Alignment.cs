using System.Numerics;
using Content.Shared._Fish.GridWelding;
using Content.Shared.Station.Components;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Fish.GridWelding;

public sealed partial class GridWeldingSystem
{
    // Проверки положения выполняются каждый тик; проверка всех тайлов нужна только перед началом и завершением.
    [Dependency] private SharedPhysicsSystem _physics = default!;

    private const float MaxSnapDistance = 1f;
    private const float SnapEpsilon = 0.001f;
    private const float MaxRelativeSpeed = 0.25f;
    private const float MaxRelativeAngularSpeed = 0.1f;
    private static readonly Vector2i[] EdgeDirections = [Vector2i.Up, Vector2i.Right, Vector2i.Down, Vector2i.Left];

    /// <summary>
    /// Maps the second grid's local coordinates into the first grid's tile lattice using a snapped quarter-turn rotation.
    /// </summary>
    internal readonly record struct WeldAlignment(Matrix3x2 Matrix, Angle Rotation);

    /// <summary>
    /// Revalidates the selected tiles, welder, reach, and relative motion, then computes a candidate alignment
    /// from the second grid into the first. The snap distance is measured at the seam on the grid that would be moved.
    /// </summary>
    /// <remarks>
    /// Runs throughout the DoAfter. Whole-grid merge restrictions are checked separately by <see cref="CanMergeGrids"/>.
    /// On failure, <paramref name="reason"/> identifies the localization key for the failed check.
    /// </remarks>
    private bool CanWeld(Entity<GridWeldingComponent> welder, EntityUid user,
        GridWeldingDoAfterEvent args, out WeldAlignment alignment, out string reason)
    {
        alignment = default;
        reason = "fish-grid-welding-unavailable";
        if (!CanUseWelder((welder, null), user))
            return false;

        var first = GetEntity(args.FirstGrid);
        var second = GetEntity(args.SecondGrid);
        reason = "fish-grid-welding-invalid";
        if (first == second || welder.Comp.User != user || welder.Comp.Grid != first || welder.Comp.Tile != args.FirstTile ||
            TerminatingOrDeleted(first) || TerminatingOrDeleted(second) ||
            !TryComp(first, out MapGridComponent? a) || !TryComp(second, out MapGridComponent? b) ||
            !TryComp(first, out PhysicsComponent? bodyA) || !TryComp(second, out PhysicsComponent? bodyB))
            return false;

        var xformA = Transform(first);
        var xformB = Transform(second);
        if (xformA.MapUid == null || xformA.MapUid != xformB.MapUid ||
            HasComp<FTLComponent>(first) || HasComp<FTLComponent>(second) ||
            xformA.ParentUid != xformA.MapUid || xformB.ParentUid != xformB.MapUid ||
            a.TileSize != 1 || b.TileSize != 1 ||
            bodyA.BodyType is not (BodyType.Dynamic or BodyType.Static) || bodyB.BodyType is not (BodyType.Dynamic or BodyType.Static) ||
            _maps.GetTileRef(first, a, args.FirstTile).Tile.IsEmpty || _maps.GetTileRef(second, b, args.SecondTile).Tile.IsEmpty)
            return false;

        reason = "fish-grid-welding-unreachable";
        if (!CanReachTile(user, first, a, args.FirstTile) || !CanReachTile(user, second, b, args.SecondTile))
            return false;

        reason = "fish-grid-welding-alignment";
        var relative = _transform.GetWorldMatrix(xformB) * _transform.GetInvWorldMatrix(xformA);
        var angle = (_transform.GetWorldRotation(xformB) - _transform.GetWorldRotation(xformA)).Reduced();
        var quarterTurns = (int) Math.Round(angle.Theta / (Math.PI / 2));
        var rotation = new Angle(quarterTurns * Math.PI / 2);

        var centerA = (Vector2) args.FirstTile + new Vector2(0.5f);
        var centerB = (Vector2) args.SecondTile + new Vector2(0.5f);

        // Ближайший четверть-оборот допускает перекос до 45 градусов и сохраняет целочисленную сетку.
        var snappedRotation = ((quarterTurns % 4 + 4) % 4) switch
        {
            1 => new Matrix3x2(0, 1, -1, 0, 0, 0),
            2 => new Matrix3x2(-1, 0, 0, -1, 0, 0),
            3 => new Matrix3x2(0, -1, 1, 0, 0, 0),
            _ => Matrix3x2.Identity,
        };
        Matrix3x2.Invert(relative, out var inverseRelative);
        var keepFirst = GetSurvivingGrid(first, second) == first;
        var donorPoint = keepFirst ? centerB : centerA;
        var actualPoint = Vector2.Transform(donorPoint, keepFirst ? relative : inverseRelative);
        const float maxDistance = MaxSnapDistance + SnapEpsilon;
        var bestDistance = maxDistance * maxDistance;
        var found = false;
        var matrix = default(Matrix3x2);
        foreach (var direction in EdgeDirections)
        {
            if (!_maps.GetTileRef(first, a, args.FirstTile + direction).Tile.IsEmpty)
                continue;

            var candidate = snappedRotation;
            candidate.Translation = centerA + (Vector2) direction - Vector2.Transform(centerB, candidate);
            var donorMatrix = candidate;
            if (!keepFirst)
                Matrix3x2.Invert(candidate, out donorMatrix);

            // Ограничиваем перемещение у шва. Поворот большого обломка больше не зависит от его дальнего края.
            var distance = Vector2.DistanceSquared(actualPoint, Vector2.Transform(donorPoint, donorMatrix));
            if (distance > bestDistance)
                continue;

            bestDistance = distance;
            matrix = candidate;
            found = true;
        }
        if (!found)
            return false;

        reason = "fish-grid-welding-motion";
        // Скорости сравниваются в одной мировой точке, чтобы общее вращение не считалось расхождением.
        var velocityA = _physics.GetLinearVelocity(first, centerA, bodyA, xformA);
        var velocityB = _physics.GetLinearVelocity(second, Vector2.Transform(centerA, inverseRelative), bodyB, xformB);
        if (Vector2.DistanceSquared(velocityA, velocityB) > MaxRelativeSpeed * MaxRelativeSpeed ||
            Math.Abs(bodyA.AngularVelocity - bodyB.AngularVelocity) > MaxRelativeAngularSpeed)
            return false;

        alignment = new WeldAlignment(matrix, rotation);
        return true;
    }

    /// <summary>
    /// Checks protection, joints, station membership, and tile overlap before a merge can consume a grid.
    /// Rejects a survivor choice that would delete a service shuttle's persistent role and UID.
    /// </summary>
    /// <remarks>
    /// Scans the consumed grid in survivor coordinates at action start and completion, rather than every tick.
    /// On failure, <paramref name="reason"/> identifies the localization key for the failed check.
    /// </remarks>
    private bool CanMergeGrids(EntityUid first, EntityUid second, WeldAlignment alignment, out string reason)
    {
        reason = "fish-grid-welding-protected";
        var survivor = GetSurvivingGrid(first, second);
        if (IsProtectedGrid(first) || IsProtectedGrid(second) || HasPersistentRole(survivor == first ? second : first) ||
            IsMagnetTarget(first, second))
            return false;

        reason = "fish-grid-welding-attached";
        if (TryComp(first, out JointComponent? jointsA) && jointsA.JointCount > 0 ||
            TryComp(second, out JointComponent? jointsB) && jointsB.JointCount > 0)
            return false;

        reason = "fish-grid-welding-station";
        if (TryComp(first, out StationMemberComponent? stationA) && TryComp(second, out StationMemberComponent? stationB) &&
            stationA.Station != stationB.Station)
            return false;

        reason = "fish-grid-welding-overlap";
        var matrix = alignment.Matrix;
        if (survivor == second)
        {
            // Проверяем тайлы переносимой части, даже если вторым был выбран большой принимающий грид.
            Matrix3x2.Invert(matrix, out matrix);
            (first, second) = (second, first);
        }

        var a = Comp<MapGridComponent>(first);
        var b = Comp<MapGridComponent>(second);
        var tiles = _maps.GetAllTiles(second, b);
        while (tiles.MoveNext(out var tile))
        {
            var destination = TransformTile(tile.Value.GridIndices, matrix);
            if (!_maps.GetTileRef(first, a, destination).Tile.IsEmpty)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Transforms a tile's center and floors the result to obtain its destination index.
    /// Using the center avoids assigning a rotated tile to the neighboring cell at an exact tile boundary.
    /// </summary>
    internal static Vector2i TransformTile(Vector2i tile, Matrix3x2 matrix)
    {
        return Vector2.Transform((Vector2) tile + new Vector2(0.5f), matrix).Floored();
    }
}
