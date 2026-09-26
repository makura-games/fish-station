using System.Numerics;
using Content.Server.Mining;
using Content.Server.Shuttles.Components;
using Content.Shared._Fish.ObrCall;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Fish.ObrCall;

public sealed partial class ObrCallSystem
{
    public static readonly ProtoId<ObrCallSettingsPrototype> DefaultSettingsId = "DefaultObrCallSettings";

    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>
    /// Ищет безопасную точку ~arrivalDistance от станции и отправляет шаттл туда через FTL (без стыковки).
    /// </summary>
    private bool TryFtlObrToDistantPoint(EntityUid shuttleUid, ShuttleComponent shuttle, EntityUid stationGrid)
    {
        if (!TryFindSafeArrivalCoordinates(shuttleUid, stationGrid, out var coords, out var angle))
            return false;

        _shuttles.FTLToCoordinates(shuttleUid, shuttle, coords, angle);
        return true;
    }

    /// <summary>
    /// Подбирает точку вокруг станции: дистанция из настроек, без метеоритов и пересечений с сетками.
    /// </summary>
    public bool TryFindSafeArrivalCoordinates(
        EntityUid shuttleUid,
        EntityUid stationGrid,
        out EntityCoordinates coordinates,
        out Angle angle)
    {
        coordinates = default;
        angle = Angle.Zero;

        if (!TryComp(stationGrid, out TransformComponent? stationXform) ||
            stationXform.MapUid is not { } mapUid ||
            !TryComp(shuttleUid, out MapGridComponent? shuttleGrid))
        {
            return false;
        }

        var settings = _prototypes.Index(DefaultSettingsId);
        var stationWorld = GetGridWorldCenter(stationGrid);
        var mapId = stationXform.MapID;
        var shuttleLocalAabb = shuttleGrid.LocalAABB;
        var size = new Vector2(
            shuttleLocalAabb.Width + settings.ClearancePadding * 2f,
            shuttleLocalAabb.Height + settings.ClearancePadding * 2f);

        for (var distance = settings.ArrivalDistance;
             distance <= settings.MaxArrivalDistance + 0.01f;
             distance += settings.DistanceStep)
        {
            for (var attempt = 0; attempt < settings.AttemptsPerRadius; attempt++)
            {
                var dir = _random.NextAngle();
                var candidateWorld = stationWorld + dir.ToWorldVec() * distance;
                var candidateBox = Box2.CenteredAround(candidateWorld, size);

                if (!IsArrivalBoxClear(mapId, shuttleUid, candidateBox))
                    continue;

                coordinates = new EntityCoordinates(mapUid, candidateWorld);
                // Ориентируем шаттл лицом к станции.
                angle = (stationWorld - candidateWorld).ToWorldAngle();
                return true;
            }
        }

        _sawmill.Warning(
            $"Failed to find safe OBR arrival point near {ToPrettyString(stationGrid)} within {settings.MaxArrivalDistance}m");
        return false;
    }

    private Vector2 GetGridWorldCenter(EntityUid gridUid)
    {
        if (TryComp(gridUid, out MapGridComponent? grid))
        {
            var localCenter = grid.LocalAABB.Center;
            return Vector2.Transform(localCenter, _transform.GetWorldMatrix(gridUid));
        }

        return _transform.GetWorldPosition(gridUid);
    }

    /// <summary>
    /// Ищет безопасную точку в секторе ЦК на расстоянии ~100 от станции ЦК и отправляет шаттл туда через FTL (без стыковки).
    /// </summary>
    private bool TryFtlObrToCentCommDistantPoint(EntityUid shuttleUid, ShuttleComponent shuttle, EntityUid centcommGrid)
    {
        if (!TryFindSafeCentCommArrivalCoordinates(shuttleUid, centcommGrid, out var coords, out var angle))
            return false;

        _shuttles.FTLToCoordinates(shuttleUid, shuttle, coords, angle);
        return true;
    }

    /// <summary>
    /// Подбирает безопасную точку в секторе ЦК на расстоянии ~100 без столкновений.
    /// </summary>
    public bool TryFindSafeCentCommArrivalCoordinates(
        EntityUid shuttleUid,
        EntityUid centcommGrid,
        out EntityCoordinates coordinates,
        out Angle angle)
    {
        coordinates = default;
        angle = Angle.Zero;

        if (!TryComp(centcommGrid, out TransformComponent? centcommXform) ||
            centcommXform.MapUid is not { } mapUid ||
            !TryComp(shuttleUid, out MapGridComponent? shuttleGrid))
        {
            return false;
        }

        var settings = _prototypes.Index(DefaultSettingsId);
        var centcommWorld = GetGridWorldCenter(centcommGrid);
        var mapId = centcommXform.MapID;
        var shuttleLocalAabb = shuttleGrid.LocalAABB;
        var size = new Vector2(
            shuttleLocalAabb.Width + settings.ClearancePadding * 2f,
            shuttleLocalAabb.Height + settings.ClearancePadding * 2f);

        for (var distance = settings.CentCommArrivalDistance;
             distance <= settings.CentCommMaxArrivalDistance + 0.01f;
             distance += settings.CentCommDistanceStep)
        {
            for (var attempt = 0; attempt < settings.CentCommAttemptsPerRadius; attempt++)
            {
                var dir = _random.NextAngle();
                var candidateWorld = centcommWorld + dir.ToWorldVec() * distance;
                var candidateBox = Box2.CenteredAround(candidateWorld, size);

                if (!IsArrivalBoxClear(mapId, shuttleUid, candidateBox))
                    continue;

                coordinates = new EntityCoordinates(mapUid, candidateWorld);
                // Ориентируем шаттл лицом к станции ЦК.
                angle = (centcommWorld - candidateWorld).ToWorldAngle();
                return true;
            }
        }

        _sawmill.Warning(
            $"Failed to find safe OBR arrival point near CentComm {ToPrettyString(centcommGrid)} within {settings.CentCommMaxArrivalDistance}m");
        return false;
    }

    private bool IsArrivalBoxClear(MapId mapId, EntityUid shuttleUid, Box2 worldBox)
    {
        // Другие сетки (станция, астероиды, шаттлы) — пересечение запрещено.
        var grids = new List<Entity<MapGridComponent>>();
        _mapManager.FindGridsIntersecting(mapId, worldBox, ref grids, includeMap: false);
        foreach (var grid in grids)
        {
            if (grid.Owner == shuttleUid)
                continue;

            return false;
        }

        // Метеориты в зоне AABB шаттла (+ padding).
        var meteors = new HashSet<Entity<MeteorComponent>>();
        _lookup.GetEntitiesIntersecting(mapId, worldBox, meteors, LookupFlags.Uncontained);
        return meteors.Count == 0;
    }
}

