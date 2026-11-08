using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server._Fish.CentCommSupply;
using Content.Server.Access.Systems;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared._Fish.CentCommSupply;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.Paper;
using Content.Shared.Storage.Components;
using NUnit.Framework;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.CentCommSupply;

[TestFixture, NonParallelizable]
public sealed class CentCommSupplyTest
{
    private const string MapProto = "Dev";

    [Test]
    public async Task OrderDeliversToSelectedStation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var ticker = server.System<GameTicker>();
        var stationSystem = server.System<StationSystem>();
        var mapSystem = server.System<MapSystem>();
        var labelSystem = server.System<LabelSystem>();
        var ui = server.System<UserInterfaceSystem>();
        var bus = entMan.EventBus;

        EntityUid console = default;
        EntityUid target = default;
        MapId devMapId = default;
        MapId consoleMapId = default;
        EntityUid actor = default;
        var targetNet = NetEntity.Invalid;
        var existingCrates = new HashSet<EntityUid>();

        await server.WaitPost(() =>
        {
            var map = protoMan.Index<GameMapPrototype>(MapProto);
            var grids = ticker.LoadGameMap(map, out devMapId, DeserializationOptions.Default with { InitializeMaps = true });
            target = stationSystem.GetOwningStation(grids[0])
                ?? throw new InvalidOperationException("Loaded Dev map has no owning station");
        });

        await server.WaitPost(() =>
        {
            var consoleMap = mapSystem.CreateMap(out consoleMapId);
            console = entMan.SpawnEntity("FishCentCommSupplyConsole", new MapCoordinates(0, 0, consoleMapId));
            entMan.GetComponent<CentCommSupplyConsoleComponent>(console).DeliveryDelay = TimeSpan.Zero;
            actor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.RemoveComponent<AccessReaderComponent>(console);
            bus.RaiseLocalEvent(console, new BoundUIOpenedEvent(CentCommSupplyUiKey.Key, console, actor));
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(ui.TryGetUiState<CentCommSupplyBoundUserInterfaceState>(
                console,
                CentCommSupplyUiKey.Key,
                out var state), Is.True);

            var names = state!.AvailableStations.Values.ToHashSet();
            Assert.That(names, Does.Contain("Dev"));
        });

        await server.WaitPost(() =>
        {
            targetNet = entMan.GetNetEntity(target);
            bus.RaiseLocalEvent(console, new CentCommSupplySelectStationMessage(targetNet)
            {
                Actor = actor,
            });
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(ui.TryGetUiState<CentCommSupplyBoundUserInterfaceState>(
                console,
                CentCommSupplyUiKey.Key,
                out var state), Is.True);
            Assert.That(state!.SelectedStation, Is.EqualTo((NetEntity?) targetNet));

            var existingQuery = entMan.AllEntityQueryEnumerator<EntityStorageComponent>();
            while (existingQuery.MoveNext(out var uid, out _))
            {
                var meta = entMan.GetComponent<MetaDataComponent>(uid);
                if (meta.EntityPrototype?.ID == "CrateCommandSecure")
                    existingCrates.Add(uid);
            }
        });

        await server.WaitPost(() =>
        {
            var entries = new List<CentCommSupplyOrderEntry>
            {
                new("FishCentCommSupplyMedicalBundle", 1),
                new("FishCentCommSupplyAsh12Duffel", 1),
                new("FishCentCommSupplyAsh12Slug", 1),
                new("FishCentCommSupplyAsh12Blueprint", 1),
            };
            bus.RaiseLocalEvent(console, new CentCommSupplyOrderMessage(Guid.NewGuid(), targetNet, entries)
            {
                Actor = actor,
            });
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(ui.TryGetUiState<CentCommSupplyBoundUserInterfaceState>(
                console,
                CentCommSupplyUiKey.Key,
                out var state), Is.True);

            var crates = new List<EntityUid>();
            var query = entMan.AllEntityQueryEnumerator<EntityStorageComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                var meta = entMan.GetComponent<MetaDataComponent>(uid);
                if (meta.EntityPrototype?.ID == "CrateCommandSecure" && !existingCrates.Contains(uid))
                    crates.Add(uid);
            }

            Assert.That(crates, Is.Not.Empty, state!.StatusMessage);

            var products = new List<string>();
            foreach (var crate in crates)
            {
                Assert.That(stationSystem.GetOwningStation(crate), Is.EqualTo(target));

                Assert.That(labelSystem.TryGetLabel<PaperComponent>(crate, out var label), Is.True,
                    "CentComm supply invoice was not attached to the crate");
                Assert.That(label!.Value.Comp.Content, Is.Not.Empty, "CentComm supply invoice is blank");
                Assert.That(label.Value.Comp.Content, Does.Contain("\n"),
                    "CentComm supply invoice contains only one line");
                Assert.That(label.Value.Comp.Content, Does.Not.Contain("centcomm-supply-paper-text"),
                    "CentComm supply invoice localization key was not resolved");
                Assert.That(label.Value.Comp.Content, Does.Not.Contain("centcomm-supply-paper-product"),
                    "CentComm supply invoice product localization key was not resolved");

                var storage = entMan.GetComponent<EntityStorageComponent>(crate);
                products.AddRange(storage.Contents.ContainedEntities
                    .Select(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID ?? string.Empty));
            }

            Assert.That(products, Is.EquivalentTo(new[]
            {
                "ClothingBackpackDuffelNanotrasenMedicalBundleFilled",
                "FishClothingBackpackDuffelWeaponAsh12BlueShield",
                "MagazineAsh12Slug",
                "BlueprintMagAsh12Basic",
            }));
        });

        await server.WaitPost(() =>
        {
            if (entMan.EntityExists(actor))
                entMan.DeleteEntity(actor);

            if (entMan.EntityExists(target))
                entMan.DeleteEntity(target);

            mapSystem.DeleteMap(consoleMapId);
            mapSystem.DeleteMap(devMapId);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeliveryRespectsConfiguredDelay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var ticker = server.System<GameTicker>();
        var stationSystem = server.System<StationSystem>();
        var mapSystem = server.System<MapSystem>();
        var bus = entMan.EventBus;

        EntityUid console = default;
        EntityUid target = default;
        MapId devMapId = default;
        MapId consoleMapId = default;
        EntityUid actor = default;
        var targetNet = NetEntity.Invalid;
        var existingCrates = new HashSet<EntityUid>();

        await server.WaitPost(() =>
        {
            var map = protoMan.Index<GameMapPrototype>(MapProto);
            var grids = ticker.LoadGameMap(map, out devMapId, DeserializationOptions.Default with { InitializeMaps = true });
            target = stationSystem.GetOwningStation(grids[0])
                ?? throw new InvalidOperationException("Loaded Dev map has no owning station");
        });

        await server.WaitPost(() =>
        {
            var consoleMap = mapSystem.CreateMap(out consoleMapId);
            console = entMan.SpawnEntity("FishCentCommSupplyConsole", new MapCoordinates(0, 0, consoleMapId));
            entMan.GetComponent<CentCommSupplyConsoleComponent>(console).DeliveryDelay = TimeSpan.FromSeconds(1);
            actor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.RemoveComponent<AccessReaderComponent>(console);
            bus.RaiseLocalEvent(console, new BoundUIOpenedEvent(CentCommSupplyUiKey.Key, console, actor));
        });

        await server.WaitPost(() =>
        {
            targetNet = entMan.GetNetEntity(target);
            bus.RaiseLocalEvent(console, new CentCommSupplySelectStationMessage(targetNet) { Actor = actor });
        });

        await server.WaitAssertion(() =>
        {
            existingCrates.Clear();
            existingCrates.UnionWith(GetNewCrates(entMan, new HashSet<EntityUid>()));
        });

        await server.WaitPost(() =>
        {
            var entries = new List<CentCommSupplyOrderEntry>
            {
                new("FishCentCommSupplyMedicalBundle", 1),
            };
            bus.RaiseLocalEvent(console, new CentCommSupplyOrderMessage(Guid.NewGuid(), targetNet, entries)
            {
                Actor = actor,
            });
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var delivered = GetNewCrates(entMan, existingCrates)
                .Where(crate => stationSystem.GetOwningStation(crate) == target)
                .ToList();

            Assert.That(delivered, Is.Empty,
                "CentComm supply crates were delivered before the configured delay");
        });

        await server.WaitRunTicks(120);

        await server.WaitAssertion(() =>
        {
            var delivered = GetNewCrates(entMan, existingCrates)
                .Where(crate => stationSystem.GetOwningStation(crate) == target)
                .ToList();

            Assert.That(delivered, Is.Not.Empty,
                "CentComm supply crates were not delivered after the configured delay");
        });

        await server.WaitPost(() =>
        {
            if (entMan.EntityExists(actor))
                entMan.DeleteEntity(actor);

            if (entMan.EntityExists(target))
                entMan.DeleteEntity(target);

            mapSystem.DeleteMap(consoleMapId);
            mapSystem.DeleteMap(devMapId);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OrderRequiresAccess()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var ticker = server.System<GameTicker>();
        var stationSystem = server.System<StationSystem>();
        var mapSystem = server.System<MapSystem>();
        var ui = server.System<UserInterfaceSystem>();
        var loc = server.ResolveDependency<ILocalizationManager>();
        var bus = entMan.EventBus;

        EntityUid console = default;
        EntityUid target = default;
        MapId devMapId = default;
        MapId consoleMapId = default;
        EntityUid actor = default;
        var targetNet = NetEntity.Invalid;

        await server.WaitPost(() =>
        {
            var map = protoMan.Index<GameMapPrototype>(MapProto);
            var grids = ticker.LoadGameMap(map, out devMapId, DeserializationOptions.Default with { InitializeMaps = true });
            target = stationSystem.GetOwningStation(grids[0])
                ?? throw new InvalidOperationException("Loaded Dev map has no owning station");
        });

        await server.WaitPost(() =>
        {
            var consoleMap = mapSystem.CreateMap(out consoleMapId);
            console = entMan.SpawnEntity("FishCentCommSupplyConsole", new MapCoordinates(0, 0, consoleMapId));
            actor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            bus.RaiseLocalEvent(console, new BoundUIOpenedEvent(CentCommSupplyUiKey.Key, console, actor));
        });

        await server.WaitPost(() =>
        {
            targetNet = entMan.GetNetEntity(target);
            bus.RaiseLocalEvent(console, new CentCommSupplySelectStationMessage(targetNet) { Actor = actor });
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(ui.TryGetUiState<CentCommSupplyBoundUserInterfaceState>(
                console,
                CentCommSupplyUiKey.Key,
                out var state), Is.True);
            Assert.That(state!.StatusMessage, Is.EqualTo(loc.GetString("centcomm-supply-error-access")));
        });

        await server.WaitPost(() =>
        {
            if (entMan.EntityExists(actor))
                entMan.DeleteEntity(actor);

            if (entMan.EntityExists(target))
                entMan.DeleteEntity(target);

            mapSystem.DeleteMap(consoleMapId);
            mapSystem.DeleteMap(devMapId);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OrderAllowedWithCentralCommandAccess()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var ticker = server.System<GameTicker>();
        var stationSystem = server.System<StationSystem>();
        var mapSystem = server.System<MapSystem>();
        var ui = server.System<UserInterfaceSystem>();
        var accessSystem = server.System<AccessSystem>();
        var bus = entMan.EventBus;

        EntityUid console = default;
        EntityUid target = default;
        MapId devMapId = default;
        MapId consoleMapId = default;
        EntityUid actor = default;
        var targetNet = NetEntity.Invalid;

        await server.WaitPost(() =>
        {
            var map = protoMan.Index<GameMapPrototype>(MapProto);
            var grids = ticker.LoadGameMap(map, out devMapId, DeserializationOptions.Default with { InitializeMaps = true });
            target = stationSystem.GetOwningStation(grids[0])
                ?? throw new InvalidOperationException("Loaded Dev map has no owning station");
        });

        await server.WaitPost(() =>
        {
            var consoleMap = mapSystem.CreateMap(out consoleMapId);
            console = entMan.SpawnEntity("FishCentCommSupplyConsole", new MapCoordinates(0, 0, consoleMapId));
            actor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var access = entMan.AddComponent<AccessComponent>(actor);
            accessSystem.TrySetTags(actor,
                new[] { new ProtoId<AccessLevelPrototype>("CentralCommand") },
                access);
            bus.RaiseLocalEvent(console, new BoundUIOpenedEvent(CentCommSupplyUiKey.Key, console, actor));
        });

        await server.WaitPost(() =>
        {
            targetNet = entMan.GetNetEntity(target);
            bus.RaiseLocalEvent(console, new CentCommSupplySelectStationMessage(targetNet) { Actor = actor });
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(ui.TryGetUiState<CentCommSupplyBoundUserInterfaceState>(
                console,
                CentCommSupplyUiKey.Key,
                out var state), Is.True);
            Assert.That(state!.StatusMessage, Is.Null);
            Assert.That(state!.SelectedStation, Is.EqualTo((NetEntity?) targetNet));
        });

        await server.WaitPost(() =>
        {
            if (entMan.EntityExists(actor))
                entMan.DeleteEntity(actor);

            if (entMan.EntityExists(target))
                entMan.DeleteEntity(target);

            mapSystem.DeleteMap(consoleMapId);
            mapSystem.DeleteMap(devMapId);
        });

        await pair.CleanReturnAsync();
    }

    private static List<EntityUid> GetNewCrates(IEntityManager entMan, HashSet<EntityUid> existing)
    {
        var crates = new List<EntityUid>();
        var query = entMan.AllEntityQueryEnumerator<EntityStorageComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            var meta = entMan.GetComponent<MetaDataComponent>(uid);
            if (meta.EntityPrototype?.ID == "CrateCommandSecure" && !existing.Contains(uid))
                crates.Add(uid);
        }

        return crates;
    }
}
