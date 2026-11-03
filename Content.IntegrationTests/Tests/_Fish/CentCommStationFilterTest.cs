using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server._Sunrise.FleshCult.Events;
using Content.Server._Sunrise.Station;
using Content.Server._Sunrise.StationCentComm;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Server.VentHorde.Components;
using Content.Server.VentHorde.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Random;

namespace Content.IntegrationTests.Tests._Fish;

[TestFixture]
[NonParallelizable]
public sealed class CentCommStationFilterTest
{
    private const string GuaranteedCritterMigrationRule = "KingRatMigration";
    private const string FleshWormsRule = "VentFleshWorms";
    private const string VentHordeRule = "SlimesSpawnHorde";
    private const string VentClogRule = "VentClog";
    private const string RandomSpawnRule = "RevenantSpawn";
    private const string MeteorSwarmRule = "SpaceDustMinor";

    [Test]
    public async Task GameRuleDoesNotChooseCentCommStation()
    {
        await using var pair = await PoolManager.GetServerClient(CreatePoolSettings());
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var ticker = server.System<GameTicker>();
        var stationSystem = server.System<StationSystem>();

        try
        {
            await server.WaitIdleAsync();
            await server.WaitAssertion(() =>
            {
                var (centcommStation, _, _, ordinaryGrid, _) = GetStations(entityManager, stationSystem);
                SetOnlyEventEligibleStation(entityManager, stationSystem, centcommStation);

                ClearVentLocations(entityManager);
                SetStationMember(entityManager, ordinaryGrid, centcommStation);
                var location = AddVentLocation(entityManager, ordinaryGrid);
                var transform = entityManager.GetComponent<TransformComponent>(location);
                Assert.That(transform.Anchored, Is.True);
                Assert.That(transform.GridUid, Is.EqualTo(ordinaryGrid));
                Assert.That(server.System<StationCentCommSystem>().IsOnCentcomm(location), Is.False);
                Assert.That(entityManager.GetComponent<StationMemberComponent>(ordinaryGrid).Station,
                    Is.EqualTo(centcommStation));
                Assert.That(stationSystem.GetStations()
                    .Where(station => entityManager.HasComponent<StationEventEligibleComponent>(station))
                    .ToArray(), Is.EqualTo(new[] { centcommStation }));

                var rule = ticker.AddGameRule(VentHordeRule);
                var horde = entityManager.GetComponent<VentHordeRuleComponent>(rule);
                Assert.That(horde.ChosenVent, Is.Null,
                    "A rule with only CentComm eligible must not select a vent on an ordinary map.");
            });
        }
        finally
        {
            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task VentCrittersDoesNotSpawnOnCentComm()
    {
        await using var pair = await PoolManager.GetServerClient(CreatePoolSettings());
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var ticker = server.System<GameTicker>();
        var stationSystem = server.System<StationSystem>();

        try
        {
            await server.WaitIdleAsync();
            await server.WaitAssertion(() =>
            {
                var (_, ordinaryStation, centcommGrid, _, centcommMapId) = GetStations(entityManager, stationSystem);
                SetOnlyEventEligibleStation(entityManager, stationSystem, ordinaryStation);
                ClearVentLocations(entityManager);
                SetStationMember(entityManager, centcommGrid, ordinaryStation);
                var location = AddVentLocation(entityManager, centcommGrid);
                var transform = entityManager.GetComponent<TransformComponent>(location);
                Assert.That(transform.Anchored, Is.True);
                Assert.That(transform.GridUid, Is.EqualTo(centcommGrid));
                Assert.That(transform.MapID, Is.EqualTo(centcommMapId));
                Assert.That(server.System<StationCentCommSystem>().IsOnCentcomm(location), Is.True);
                Assert.That(entityManager.GetComponent<StationMemberComponent>(centcommGrid).Station,
                    Is.EqualTo(ordinaryStation));

                var existing = GetEntityIds(entityManager);
                var rule = ticker.AddGameRule(GuaranteedCritterMigrationRule);
                var critters = entityManager.GetComponent<VentCrittersRuleComponent>(rule);
                Assert.That(critters.SpecialEntries, Is.Not.Empty,
                    "The rule must have a guaranteed special spawn so the test cannot pass on spawn probability.");

                var gameRule = entityManager.GetComponent<GameRuleComponent>(rule);
                gameRule.Delay = null;
                Assert.That(ticker.StartGameRule(rule, gameRule), Is.True);
                Assert.That(entityManager.HasComponent<ActiveGameRuleComponent>(rule), Is.True,
                    "The rule must start immediately so its Started handler is exercised.");

                Assert.That(CountNewEntities(entityManager, existing, centcommMapId), Is.Zero);
            });
        }
        finally
        {
            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task VentFleshWormsDoesNotSpawnOnCentComm()
    {
        await using var pair = await PoolManager.GetServerClient(CreatePoolSettings());
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var ticker = server.System<GameTicker>();
        var stationSystem = server.System<StationSystem>();

        try
        {
            await server.WaitIdleAsync();
            await server.WaitAssertion(() =>
            {
                var (_, ordinaryStation, centcommGrid, _, centcommMapId) = GetStations(entityManager, stationSystem);
                ClearVentLocations(entityManager);
                SetStationMember(entityManager, centcommGrid, ordinaryStation);
                AddVentLocation(entityManager, centcommGrid);

                stationSystem.AddGridToStation(ordinaryStation, centcommGrid);

                var existing = GetEntityIds(entityManager);
                var rule = ticker.AddGameRule(FleshWormsRule);
                var worms = entityManager.GetComponent<VentFleshWormsRuleComponent>(rule);
                ticker.StartGameRule(rule);

                Assert.That(CountNewEntities(
                    entityManager,
                    existing,
                    centcommMapId,
                    worms.SpawnedPrototypeWorm), Is.Zero);
            });
        }
        finally
        {
            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task VentHordeDoesNotSpawnOnCentComm()
    {
        await using var pair = await PoolManager.GetServerClient(CreatePoolSettings());
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var ticker = server.System<GameTicker>();
        var stationSystem = server.System<StationSystem>();
        var hordeSystem = server.System<VentHordeSystem>();

        try
        {
            await server.WaitIdleAsync();
            await server.WaitAssertion(() =>
            {
                var (_, ordinaryStation, centcommGrid, _, centcommMapId) = GetStations(entityManager, stationSystem);
                SetOnlyEventEligibleStation(entityManager, stationSystem, ordinaryStation);
                ClearVentLocations(entityManager);
                SetStationMember(entityManager, centcommGrid, ordinaryStation);
                var vent = AddVentLocation(entityManager, centcommGrid);

                var existing = GetEntityIds(entityManager);
                var rule = ticker.AddGameRule(VentHordeRule);
                var horde = entityManager.GetComponent<VentHordeRuleComponent>(rule);
                ticker.StartGameRule(rule);

                if (entityManager.TryGetComponent<VentHordeSpawnerComponent>(vent, out var spawner))
                    hordeSystem.EndHordeSpawn((vent, spawner));

                Assert.That(horde.ChosenVent, Is.Null);
                Assert.That(CountNewEntities(entityManager, existing, centcommMapId), Is.Zero);
            });
        }
        finally
        {
            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task VentClogDoesNotSpawnOnCentComm()
    {
        await using var pair = await PoolManager.GetServerClient(CreatePoolSettings());
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var ticker = server.System<GameTicker>();
        var stationSystem = server.System<StationSystem>();

        try
        {
            await server.WaitIdleAsync();
            await server.WaitAssertion(() =>
            {
                var (_, ordinaryStation, centcommGrid, _, centcommMapId) = GetStations(entityManager, stationSystem);
                SetOnlyEventEligibleStation(entityManager, stationSystem, ordinaryStation);
                SetStationMember(entityManager, centcommGrid, ordinaryStation);

                var existingVents = entityManager.EntityQuery<GasVentPumpComponent>()
                    .Select(component => component.Owner)
                    .ToArray();
                foreach (var existingVent in existingVents)
                {
                    entityManager.RemoveComponent<GasVentPumpComponent>(existingVent);
                }

                var vent = entityManager.SpawnEntity(null, new EntityCoordinates(centcommGrid, Vector2.Zero));
                entityManager.EnsureComponent<GasVentPumpComponent>(vent);
                var transform = entityManager.GetComponent<TransformComponent>(vent);
                Assert.That(transform.GridUid, Is.EqualTo(centcommGrid));
                Assert.That(transform.MapID, Is.EqualTo(centcommMapId));
                Assert.That(server.System<StationCentCommSystem>().IsOnCentcomm(vent), Is.True);
                Assert.That(entityManager.GetComponent<StationMemberComponent>(centcommGrid).Station,
                    Is.EqualTo(ordinaryStation));

                var existing = GetEntityIds(entityManager);
                var rule = ticker.AddGameRule(VentClogRule);
                var gameRule = entityManager.GetComponent<GameRuleComponent>(rule);
                gameRule.Delay = null;
                var random = server.ResolveDependency<IRobustRandom>();
                random.SetSeed(FindVentClogSpawnSeed(random));
                Assert.That(ticker.StartGameRule(rule, gameRule), Is.True);
                Assert.That(entityManager.HasComponent<ActiveGameRuleComponent>(rule), Is.True,
                    "The rule must start immediately so its Started handler is exercised.");

                Assert.That(CountNewEntities(entityManager, existing, centcommMapId), Is.Zero);
            });
        }
        finally
        {
            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task RandomSpawnRuleDoesNotChooseCentCommGrid()
    {
        await using var pair = await PoolManager.GetServerClient(CreatePoolSettings());
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var ticker = server.System<GameTicker>();
        var stationSystem = server.System<StationSystem>();
        var mapSystem = server.System<SharedMapSystem>();

        try
        {
            await server.WaitIdleAsync();
            await server.WaitAssertion(() =>
            {
                var (_, ordinaryStation, centcommGrid, _, centcommMapId) = GetStations(entityManager, stationSystem);
                SetOnlyEventEligibleStation(entityManager, stationSystem, ordinaryStation);

                var ordinaryGrids = entityManager.GetComponent<StationDataComponent>(ordinaryStation).Grids.ToArray();
                foreach (var grid in ordinaryGrids)
                {
                    if (entityManager.GetComponent<TransformComponent>(grid).MapID == centcommMapId)
                        continue;

                    var gridComponent = entityManager.GetComponent<MapGridComponent>(grid);
                    foreach (var tile in mapSystem.GetAllTiles(grid, gridComponent).ToArray())
                    {
                        mapSystem.SetTile((grid, gridComponent), tile.GridIndices, Tile.Empty);
                    }
                }

                stationSystem.AddGridToStation(ordinaryStation, centcommGrid);

                var existing = GetEntityIds(entityManager);
                var rule = ticker.AddGameRule(RandomSpawnRule);
                ticker.StartGameRule(rule);

                Assert.That(CountNewEntities(entityManager, existing, centcommMapId), Is.Zero,
                    "A random-spawn rule must not choose a CentComm grid.");
            });
        }
        finally
        {
            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task MeteorSwarmDoesNotSpawnOnCentCommGrid()
    {
        await using var pair = await PoolManager.GetServerClient(CreatePoolSettings());
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var ticker = server.System<GameTicker>();
        var stationSystem = server.System<StationSystem>();
        var mapSystem = server.System<SharedMapSystem>();

        try
        {
            await server.WaitIdleAsync();
            HashSet<EntityUid> existing = new();
            MapId centcommMapId = MapId.Nullspace;
            EntityUid rule = EntityUid.Invalid;
            await server.WaitAssertion(() =>
            {
                var (_, ordinaryStation, centcommGrid, _, mapId) = GetStations(entityManager, stationSystem);
                centcommMapId = mapId;

                foreach (var station in stationSystem.GetStations().ToArray())
                {
                    if (entityManager.HasComponent<StationMeteorSwarmTargetComponent>(station))
                        entityManager.RemoveComponent<StationMeteorSwarmTargetComponent>(station);
                }

                entityManager.EnsureComponent<StationMeteorSwarmTargetComponent>(ordinaryStation);
                var stationData = entityManager.GetComponent<StationDataComponent>(ordinaryStation);
                var ordinaryGrid = stationData.Grids.First(grid =>
                    entityManager.GetComponent<TransformComponent>(grid).MapID != centcommMapId);
                var ordinaryGridComponent = entityManager.GetComponent<MapGridComponent>(ordinaryGrid);
                foreach (var tile in mapSystem.GetAllTiles(ordinaryGrid, ordinaryGridComponent).ToArray())
                {
                    mapSystem.SetTile((ordinaryGrid, ordinaryGridComponent), tile.GridIndices, Tile.Empty);
                }

                foreach (var grid in stationData.Grids.Where(grid => grid != ordinaryGrid).ToArray())
                {
                    stationSystem.RemoveGridFromStation(ordinaryStation, grid);
                }

                stationSystem.AddGridToStation(ordinaryStation, centcommGrid);

                Assert.That(stationSystem.GetLargestGrid(ordinaryStation), Is.EqualTo(centcommGrid),
                    "The test station must select the CentComm grid as its largest grid.");

                existing = GetEntityIds(entityManager);
                rule = ticker.AddGameRule(MeteorSwarmRule);
                Assert.That(ticker.StartGameRule(rule), Is.True);
            });

            await pair.RunTicksSync(3);

            await server.WaitAssertion(() =>
            {
                Assert.That(CountNewEntities(entityManager, existing, centcommMapId), Is.Zero);
                Assert.That(entityManager.GetComponent<MeteorSwarmComponent>(rule).WaveCounter, Is.Zero);
                Assert.That(entityManager.HasComponent<ActiveGameRuleComponent>(rule), Is.False);
                Assert.That(entityManager.HasComponent<EndedGameRuleComponent>(rule), Is.True);
            });
        }
        finally
        {
            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task MeteorSwarmSpawnsOnOrdinaryStationGrid()
    {
        await using var pair = await PoolManager.GetServerClient(CreatePoolSettings());
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var ticker = server.System<GameTicker>();
        var stationSystem = server.System<StationSystem>();

        try
        {
            await server.WaitIdleAsync();
            HashSet<EntityUid> existing = new();
            MapId stationMapId = MapId.Nullspace;
            await server.WaitAssertion(() =>
            {
                var (_, ordinaryStation, _, _, centcommMapId) = GetStations(entityManager, stationSystem);

                foreach (var station in stationSystem.GetStations().ToArray())
                {
                    if (entityManager.HasComponent<StationMeteorSwarmTargetComponent>(station))
                        entityManager.RemoveComponent<StationMeteorSwarmTargetComponent>(station);
                }

                entityManager.EnsureComponent<StationMeteorSwarmTargetComponent>(ordinaryStation);

                var targetGrid = stationSystem.GetLargestGrid(ordinaryStation)
                    ?? throw new InvalidOperationException("The ordinary station must have a largest grid.");

                stationMapId = entityManager.GetComponent<TransformComponent>(targetGrid).MapID;
                Assert.That(stationMapId, Is.Not.EqualTo(centcommMapId));
                Assert.That(server.System<StationCentCommSystem>().IsOnCentcomm(targetGrid), Is.False);

                existing = GetEntityIds(entityManager);
                var rule = ticker.AddGameRule(MeteorSwarmRule);
                Assert.That(ticker.StartGameRule(rule), Is.True);
            });

            await pair.RunTicksSync(3);

            await server.WaitAssertion(() =>
                Assert.That(CountNewEntities(entityManager, existing, stationMapId, "MeteorSpaceDust"), Is.GreaterThan(0)));
        }
        finally
        {
            await pair.CleanReturnAsync();
        }
    }

    private static PoolSettings CreatePoolSettings()
    {
        return new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
            Map = PoolManager.TestStation
        };
    }

    private static (EntityUid CentCommStation, EntityUid OrdinaryStation, EntityUid CentCommGrid,
        EntityUid OrdinaryGrid, MapId CentCommMapId) GetStations(IEntityManager entityManager, StationSystem stationSystem)
    {
        var stations = stationSystem.GetStations().ToArray();
        var centcommMapId = stations
            .Select(station => entityManager.TryGetComponent<StationCentCommComponent>(station, out var link)
                ? link.MapId
                : MapId.Nullspace)
            .First(mapId => mapId != MapId.Nullspace);
        var centcommStation = stations.Single(station =>
        {
            var grids = entityManager.GetComponent<StationDataComponent>(station).Grids;
            return grids.Count > 0 && grids.All(grid =>
                entityManager.GetComponent<TransformComponent>(grid).MapID == centcommMapId);
        });
        var ordinaryStation = stations.Single(station =>
            station != centcommStation &&
            entityManager.HasComponent<StationEventEligibleComponent>(station));
        var ordinaryGrids = entityManager.GetComponent<StationDataComponent>(ordinaryStation).Grids;
        var centcommGrids = entityManager.GetComponent<StationDataComponent>(centcommStation).Grids;
        var centcommGrid = centcommGrids.First();
        var ordinaryGrid = ordinaryGrids.First(grid =>
            entityManager.GetComponent<TransformComponent>(grid).MapID != centcommMapId);

        return (centcommStation, ordinaryStation, centcommGrid, ordinaryGrid, centcommMapId);
    }

    private static void SetOnlyEventEligibleStation(
        IEntityManager entityManager,
        StationSystem stationSystem,
        EntityUid eligibleStation)
    {
        foreach (var station in stationSystem.GetStations().ToArray())
        {
            if (station == eligibleStation)
                entityManager.EnsureComponent<StationEventEligibleComponent>(station);
            else if (entityManager.HasComponent<StationEventEligibleComponent>(station))
                entityManager.RemoveComponent<StationEventEligibleComponent>(station);
        }
    }

    private static void SetStationMember(IEntityManager entityManager, EntityUid grid, EntityUid station)
    {
        entityManager.EnsureComponent<StationMemberComponent>(grid).Station = station;
    }

    private static EntityUid AddVentLocation(IEntityManager entityManager, EntityUid grid)
    {
        var vent = entityManager.SpawnEntity(null, new EntityCoordinates(grid, Vector2.Zero));
        entityManager.EnsureComponent<VentCritterSpawnLocationComponent>(vent);
        var transform = entityManager.GetComponent<TransformComponent>(vent);
        entityManager.System<SharedTransformSystem>().AnchorEntity(vent, transform);
        return vent;
    }

    private static void ClearVentLocations(IEntityManager entityManager)
    {
        var locations = entityManager.EntityQuery<VentCritterSpawnLocationComponent>()
            .Select(component => component.Owner)
            .ToArray();

        foreach (var location in locations)
        {
            entityManager.RemoveComponent<VentCritterSpawnLocationComponent>(location);
        }
    }

    private static int FindVentClogSpawnSeed(IRobustRandom random)
    {
        for (var seed = 0; seed < 1000; seed++)
        {
            random.SetSeed(seed);
            random.Next(1);
            if (!random.Prob(0.33f))
                continue;

            return seed;
        }

        throw new InvalidOperationException("Could not find a random seed that selects the vent for VentClog.");
    }

    private static HashSet<EntityUid> GetEntityIds(IEntityManager entityManager)
    {
        return entityManager.EntityQuery<MetaDataComponent>()
            .Select(component => component.Owner)
            .ToHashSet();
    }

    private static int CountNewEntities(
        IEntityManager entityManager,
        HashSet<EntityUid> existing,
        MapId? mapId = null,
        string prototype = null)
    {
        var count = 0;
        foreach (var metadata in entityManager.EntityQuery<MetaDataComponent>())
        {
            var uid = metadata.Owner;
            if (existing.Contains(uid) || prototype != null && metadata.EntityPrototype?.ID != prototype)
                continue;

            if (mapId != null &&
                (!entityManager.TryGetComponent<TransformComponent>(uid, out var transform) ||
                 transform.MapID != mapId.Value))
            {
                continue;
            }

            count++;
        }

        return count;
    }
}
