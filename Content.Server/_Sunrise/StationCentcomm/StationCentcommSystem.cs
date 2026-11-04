using Content.Server._Sunrise.StationCentComm;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Shuttles.Systems;
using Content.Shared._Sunrise.AlwaysPoweredMap;
using Content.Shared.Maps;
using Content.Shared.Station.Components; // Fish-edit: проверка корня станции в Nullspace.
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.StationCentComm;

public sealed partial class StationCentCommSystem : EntitySystem
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private MapSystem _map = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("station.centcomm");
        SubscribeLocalEvent<StationCentCommComponent, ComponentShutdown>(OnCentcommShutdown);
        SubscribeLocalEvent<StationCentCommComponent, ComponentInit>(OnCentcommInit);
    }

    // Fish-start: определяет принадлежность по карте, так как компонент также есть у обычной станции.
    public bool IsOnCentcomm(EntityUid uid)
    {
        if (!TryComp<TransformComponent>(uid, out var xform))
    return false;

var entityMapId = xform.MapID;
        StationDataComponent? stationData = null;
        if (entityMapId == MapId.Nullspace)
            TryComp(uid, out stationData);

        var query = EntityQueryEnumerator<StationCentCommComponent>();
        while (query.MoveNext(out _, out var centcomm))
        {
            if (centcomm.MapId == MapId.Nullspace)
                continue;

            if (entityMapId != MapId.Nullspace)
            {
                if (centcomm.MapId == entityMapId)
                    return true;

                continue;
            }

            // Корень станции считаем ЦК только если все его гриды находятся на одной карте ЦК; шаттл ЦК не должен переклассифицировать обычную станцию.
            if (stationData == null || stationData.Grids.Count == 0)
                continue;

            var allGridsOnCentcommMap = true;
            foreach (var grid in stationData.Grids)
            {
                if (TryComp(grid, out TransformComponent? gridTransform)
                    && gridTransform.MapID == centcomm.MapId)
                    continue;

                allGridsOnCentcommMap = false;
                break;
            }

            if (allGridsOnCentcommMap)
                return true;
        }

        return false;
    }
    // Fish-end

    private void OnCentcommShutdown(EntityUid uid, StationCentCommComponent component, ComponentShutdown args)
    {
        QueueDel(component.Entity);
        component.Entity = EntityUid.Invalid;

        if (_map.MapExists(component.MapId))
            _map.DeleteMap(component.MapId);

        component.MapId = MapId.Nullspace;
    }

    private void OnCentcommInit(EntityUid uid, StationCentCommComponent component, ComponentInit args)
    {
        // Post mapinit? fancy
        if (TryComp(component.Entity, out TransformComponent? xform))
        {
            component.MapId = xform.MapID;
            return;
        }

        AddCentcomm(component);
    }

    private void AddCentcomm(StationCentCommComponent component)
    {
        var query = AllEntityQuery<StationCentCommComponent>();

        while (query.MoveNext(out var otherComp))
        {
            if (otherComp == component)
                continue;

            component.MapId = otherComp.MapId;
            return;
        }

        if (ProtoMan.TryIndex<GameMapPrototype>(component.Station, out var gameMap))
        {
            _gameTicker.LoadGameMap(gameMap, out var mapId);
            component.MapId = mapId;

            var mapEnt = _map.GetMapOrInvalid(mapId);

                if (_shuttle.TryAddFTLDestination(mapId, true, out var ftlDestination))
                //Fish-start
                {
                    ftlDestination.Whitelist = component.ShuttleWhitelist;
                    ftlDestination.RequireCoordinateDisk = false;
                }
                //Fish-end

            EnsureComp<AlwaysPoweredMapComponent>(mapEnt);

            _map.InitializeMap(mapId);
        }
        else
        {
            _sawmill.Warning("No Centcomm map found, skipping setup.");
        }
    }
}
