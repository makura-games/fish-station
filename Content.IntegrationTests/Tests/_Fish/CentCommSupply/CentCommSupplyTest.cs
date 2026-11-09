using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client._Fish.CentCommSupply;
using Content.Server._Fish.CentCommSupply;
using Content.Server.Access.Systems;
using Content.Server.Cargo.Components;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared._Fish.CentCommSupply;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.Paper;
using Content.Shared.Physics;
using Content.Shared.Storage.Components;
using NUnit.Framework;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

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
        var accessSystem = server.System<AccessSystem>();
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
            var consoleComponent = entMan.GetComponent<CentCommSupplyConsoleComponent>(console);
            consoleComponent.DeliveryDelay = TimeSpan.Zero;
            consoleComponent.DeliveryTimeout = TimeSpan.Zero;
            actor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            GrantCentralCommandAccess(entMan, accessSystem, actor);
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

        var selectionId = Guid.NewGuid();
        await server.WaitPost(() =>
        {
            targetNet = entMan.GetNetEntity(target);
            bus.RaiseLocalEvent(console, new CentCommSupplySelectStationMessage(targetNet, selectionId)
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
        var accessSystem = server.System<AccessSystem>();
        var bus = entMan.EventBus;

        EntityUid console = default;
        EntityUid target = default;
        MapId devMapId = default;
        MapId consoleMapId = default;
        EntityUid actor = default;
        var targetNet = NetEntity.Invalid;
        var existingCrates = new HashSet<EntityUid>();
        var deliveryDelay = TimeSpan.FromSeconds(2);

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
            entMan.GetComponent<CentCommSupplyConsoleComponent>(console).DeliveryDelay = deliveryDelay;
            actor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            GrantCentralCommandAccess(entMan, accessSystem, actor);
            bus.RaiseLocalEvent(console, new BoundUIOpenedEvent(CentCommSupplyUiKey.Key, console, actor));
        });

        await server.WaitPost(() =>
        {
            targetNet = entMan.GetNetEntity(target);
            bus.RaiseLocalEvent(console, new CentCommSupplySelectStationMessage(targetNet, Guid.NewGuid()) { Actor = actor });
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

        var tickPeriod = server.Timing.TickPeriod;
        var ticksBeforeExpiry = Math.Max(1,
            (int) Math.Floor((deliveryDelay - tickPeriod).TotalSeconds / tickPeriod.TotalSeconds));

        await server.WaitRunTicks(ticksBeforeExpiry);

        await server.WaitAssertion(() =>
        {
            var delivered = GetNewCrates(entMan, existingCrates)
                .Where(crate => stationSystem.GetOwningStation(crate) == target)
                .ToList();

            Assert.That(delivered, Is.Empty,
                "CentComm supply crates were delivered before the configured delay");
        });

        await server.WaitRunTicks(3);

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

        var selectionId = Guid.NewGuid();
        await server.WaitPost(() =>
        {
            targetNet = entMan.GetNetEntity(target);
            bus.RaiseLocalEvent(console, new CentCommSupplySelectStationMessage(targetNet, selectionId) { Actor = actor });
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
            // Fish-start: сброс устаревшего UI-state перед отправкой нового запроса заказа
            ui.SetUiState(console, CentCommSupplyUiKey.Key, new CentCommSupplyBoundUserInterfaceState(
                new List<CentCommSupplyProductEntry>(),
                new Dictionary<NetEntity, string>(),
                null,
                6,
                8,
                null,
                false));
            // Fish-end

            var entries = new List<CentCommSupplyOrderEntry>
            {
                new("FishCentCommSupplyMedicalBundle", 1),
            };
            bus.RaiseLocalEvent(console, new CentCommSupplyOrderMessage(Guid.NewGuid(), targetNet, entries)
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
            GrantCentralCommandAccess(entMan, accessSystem, actor);
            bus.RaiseLocalEvent(console, new BoundUIOpenedEvent(CentCommSupplyUiKey.Key, console, actor));
        });

        var selectionId = Guid.NewGuid();
        await server.WaitPost(() =>
        {
            targetNet = entMan.GetNetEntity(target);
            bus.RaiseLocalEvent(console, new CentCommSupplySelectStationMessage(targetNet, selectionId) { Actor = actor });
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(ui.TryGetUiState<CentCommSupplyBoundUserInterfaceState>(
                console,
                CentCommSupplyUiKey.Key,
                out var state), Is.True);
            Assert.That(state!.StatusMessage, Is.Null);
            Assert.That(state!.SelectedStation, Is.EqualTo((NetEntity?) targetNet));

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

            Assert.That(delivered, Is.Not.Empty,
                "CentComm supply order was not delivered with CentralCommand access");
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
    public async Task StationSelectionRejectsInvalidStation()
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
        var loc = server.ResolveDependency<ILocalizationManager>();
        var bus = entMan.EventBus;

        EntityUid console = default;
        EntityUid target = default;
        MapId devMapId = default;
        MapId consoleMapId = default;
        EntityUid actor = default;

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
            GrantCentralCommandAccess(entMan, accessSystem, actor);
            bus.RaiseLocalEvent(console, new BoundUIOpenedEvent(CentCommSupplyUiKey.Key, console, actor));
        });

        var invalidSelectionId = Guid.NewGuid();
        await server.WaitPost(() =>
        {
            bus.RaiseLocalEvent(console, new CentCommSupplySelectStationMessage(NetEntity.Invalid, invalidSelectionId) { Actor = actor });
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(ui.TryGetUiState<CentCommSupplyBoundUserInterfaceState>(
                console,
                CentCommSupplyUiKey.Key,
                out var state), Is.True);
            Assert.That(state!.SelectedStation, Is.Not.EqualTo((NetEntity?) NetEntity.Invalid));
            Assert.That(state.StatusMessage, Is.EqualTo(loc.GetString("centcomm-supply-error-invalid-station")));
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
    public async Task OrderTimesOutWhenPalletsOccupied()
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
        var loc = server.ResolveDependency<ILocalizationManager>();
        var bus = entMan.EventBus;

        EntityUid console = default;
        EntityUid target = default;
        MapId devMapId = default;
        MapId consoleMapId = default;
        EntityUid actor = default;
        var targetNet = NetEntity.Invalid;
        var delay = TimeSpan.FromSeconds(1);
        var timeout = TimeSpan.FromSeconds(1);

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
            var consoleComp = entMan.GetComponent<CentCommSupplyConsoleComponent>(console);
            consoleComp.DeliveryDelay = delay;
            consoleComp.DeliveryTimeout = timeout;
            actor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            GrantCentralCommandAccess(entMan, accessSystem, actor);
            bus.RaiseLocalEvent(console, new BoundUIOpenedEvent(CentCommSupplyUiKey.Key, console, actor));
        });

        await server.WaitPost(() =>
        {
            targetNet = entMan.GetNetEntity(target);
            bus.RaiseLocalEvent(console, new CentCommSupplySelectStationMessage(targetNet, Guid.NewGuid()) { Actor = actor });
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

            // Block buy cargo pallets with solid obstacle crates after order is queued
            var query = entMan.AllEntityQueryEnumerator<CargoPalletComponent, TransformComponent>();
            while (query.MoveNext(out _, out var pallet, out var xform))
            {
                if ((pallet.PalletType & BuySellType.Buy) != 0)
                {
                    entMan.SpawnEntity("CrateCommandSecure", xform.Coordinates);
                }
            }
        });

        var tickPeriod = server.Timing.TickPeriod;
        var totalWait = delay + timeout + TimeSpan.FromSeconds(6);
        var ticks = Math.Max(1, (int) Math.Ceiling(totalWait.TotalSeconds / tickPeriod.TotalSeconds));
        await server.WaitRunTicks(ticks);

        await server.WaitAssertion(() =>
        {
            Assert.That(ui.TryGetUiState<CentCommSupplyBoundUserInterfaceState>(
                console,
                CentCommSupplyUiKey.Key,
                out var state), Is.True);
            Assert.That(state!.StatusMessage, Is.EqualTo(loc.GetString("centcomm-supply-error-delivery-timeout")));
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
    public async Task ExpiredOrderDoesNotDeliverAfterPalletsFree()
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
        var system = server.System<CentCommSupplyConsoleSystem>();
        var loc = server.ResolveDependency<ILocalizationManager>();
        var bus = entMan.EventBus;

        EntityUid console = default;
        EntityUid target = default;
        MapId devMapId = default;
        MapId consoleMapId = default;
        EntityUid actor = default;
        var targetNet = NetEntity.Invalid;
        var existingCrates = new HashSet<EntityUid>();
        var obstacles = new List<EntityUid>();
        Entity<CentCommSupplyConsoleComponent> consoleEnt = default;

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
            var consoleComp = entMan.GetComponent<CentCommSupplyConsoleComponent>(console);
            consoleComp.DeliveryDelay = TimeSpan.Zero;
            consoleComp.DeliveryTimeout = TimeSpan.FromSeconds(1);
            consoleComp.OrderCooldown = TimeSpan.Zero;
            consoleEnt = (console, consoleComp);
            actor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            GrantCentralCommandAccess(entMan, accessSystem, actor);
            targetNet = entMan.GetNetEntity(target);
            bus.RaiseLocalEvent(console, new BoundUIOpenedEvent(CentCommSupplyUiKey.Key, console, actor));
            bus.RaiseLocalEvent(console, new CentCommSupplySelectStationMessage(targetNet, Guid.NewGuid()) { Actor = actor });
        });

        await server.WaitAssertion(() =>
        {
            existingCrates.UnionWith(GetNewCrates(entMan, new HashSet<EntityUid>()));
        });

        var orderId = Guid.NewGuid();
        var placed = false;
        await server.WaitPost(() =>
        {
            var entries = new List<CentCommSupplyOrderEntry>
            {
                new("FishCentCommSupplyMedicalBundle", 1),
            };
            placed = system.TryPlaceOrder(consoleEnt, actor, orderId, targetNet, entries);

            foreach (var pallet in GetBuyPallets(entMan, stationSystem, target))
            {
                var xform = entMan.GetComponent<TransformComponent>(pallet);
                obstacles.Add(entMan.SpawnEntity("CrateGeneric", xform.Coordinates));
            }
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(placed, Is.True, "CentComm supply order was not accepted");
        });

        await server.WaitRunTicks(SecondsToTicks(server.Timing.TickPeriod, 2));

        await server.WaitAssertion(() =>
        {
            Assert.That(ui.TryGetUiState<CentCommSupplyBoundUserInterfaceState>(
                console,
                CentCommSupplyUiKey.Key,
                out var state), Is.True);
            Assert.That(state!.StatusMessage, Is.EqualTo(loc.GetString("centcomm-supply-error-delivery-timeout")));
            Assert.That(GetNewCrates(entMan, existingCrates), Is.Empty,
                "Expired order crates were not rolled back");
        });

        await server.WaitPost(() =>
        {
            foreach (var obstacle in obstacles)
            {
                if (entMan.EntityExists(obstacle))
                    entMan.DeleteEntity(obstacle);
            }
            obstacles.Clear();
        });

        await server.WaitRunTicks(SecondsToTicks(server.Timing.TickPeriod, 7));

        await server.WaitAssertion(() =>
        {
            Assert.That(GetNewCrates(entMan, existingCrates), Is.Empty,
                "Expired order was delivered after pallets became free");
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
    public async Task DeletedConsoleCleansUpTimedOutOrder()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var ticker = server.System<GameTicker>();
        var stationSystem = server.System<StationSystem>();
        var mapSystem = server.System<MapSystem>();
        var accessSystem = server.System<AccessSystem>();
        var system = server.System<CentCommSupplyConsoleSystem>();
        var bus = entMan.EventBus;

        EntityUid console = default;
        EntityUid target = default;
        MapId devMapId = default;
        MapId consoleMapId = default;
        EntityUid actor = default;
        var targetNet = NetEntity.Invalid;
        var existingCrates = new HashSet<EntityUid>();
        Entity<CentCommSupplyConsoleComponent> consoleEnt = default;

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
            var consoleComp = entMan.GetComponent<CentCommSupplyConsoleComponent>(console);
            consoleComp.DeliveryDelay = TimeSpan.Zero;
            consoleComp.DeliveryTimeout = TimeSpan.FromSeconds(1);
            consoleComp.OrderCooldown = TimeSpan.Zero;
            consoleEnt = (console, consoleComp);
            actor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            GrantCentralCommandAccess(entMan, accessSystem, actor);
            targetNet = entMan.GetNetEntity(target);
            bus.RaiseLocalEvent(console, new BoundUIOpenedEvent(CentCommSupplyUiKey.Key, console, actor));
            bus.RaiseLocalEvent(console, new CentCommSupplySelectStationMessage(targetNet, Guid.NewGuid()) { Actor = actor });
        });

        await server.WaitAssertion(() =>
        {
            existingCrates.UnionWith(GetNewCrates(entMan, new HashSet<EntityUid>()));
        });

        var placed = false;
        await server.WaitPost(() =>
        {
            var entries = new List<CentCommSupplyOrderEntry>
            {
                new("FishCentCommSupplyMedicalBundle", 1),
            };
            placed = system.TryPlaceOrder(consoleEnt, actor, Guid.NewGuid(), targetNet, entries);

            var query = entMan.AllEntityQueryEnumerator<CargoPalletComponent, TransformComponent>();
            while (query.MoveNext(out _, out var component, out var xform))
            {
                if ((component.PalletType & BuySellType.Buy) != 0)
                    entMan.SpawnEntity("CrateGeneric", xform.Coordinates);
            }

            entMan.DeleteEntity(console);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(placed, Is.True, "CentComm supply order was not accepted");
        });

        await server.WaitRunTicks(SecondsToTicks(server.Timing.TickPeriod, 2));

        await server.WaitAssertion(() =>
        {
            Assert.That(GetNewCrates(entMan, existingCrates), Is.Empty,
                "Timed out order with a deleted console left crates behind");
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
    public async Task OrderIdsAreValidated()
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
        var system = server.System<CentCommSupplyConsoleSystem>();
        var loc = server.ResolveDependency<ILocalizationManager>();
        var bus = entMan.EventBus;

        EntityUid console = default;
        EntityUid target = default;
        MapId devMapId = default;
        MapId consoleMapId = default;
        EntityUid actor = default;
        var targetNet = NetEntity.Invalid;
        Entity<CentCommSupplyConsoleComponent> consoleEnt = default;

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
            var consoleComp = entMan.GetComponent<CentCommSupplyConsoleComponent>(console);
            consoleComp.DeliveryDelay = TimeSpan.Zero;
            consoleComp.DeliveryTimeout = TimeSpan.Zero;
            consoleComp.OrderCooldown = TimeSpan.Zero;
            consoleEnt = (console, consoleComp);
            actor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            GrantCentralCommandAccess(entMan, accessSystem, actor);
            targetNet = entMan.GetNetEntity(target);
            bus.RaiseLocalEvent(console, new BoundUIOpenedEvent(CentCommSupplyUiKey.Key, console, actor));
            bus.RaiseLocalEvent(console, new CentCommSupplySelectStationMessage(targetNet, Guid.NewGuid()) { Actor = actor });
        });

        var emptyIdResult = true;
        await server.WaitPost(() =>
        {
            var entries = new List<CentCommSupplyOrderEntry>
            {
                new("FishCentCommSupplyMedicalBundle", 1),
            };
            emptyIdResult = system.TryPlaceOrder(consoleEnt, actor, Guid.Empty, targetNet, entries);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(emptyIdResult, Is.False);
            Assert.That(ui.TryGetUiState<CentCommSupplyBoundUserInterfaceState>(
                console,
                CentCommSupplyUiKey.Key,
                out var state), Is.True);
            Assert.That(state!.StatusMessage, Is.EqualTo(loc.GetString("centcomm-supply-error-invalid-order-id")));
        });

        var orderId = Guid.NewGuid();
        var acceptedResult = false;
        var duplicateResult = true;
        await server.WaitPost(() =>
        {
            var entries = new List<CentCommSupplyOrderEntry>
            {
                new("FishCentCommSupplyMedicalBundle", 1),
            };
            acceptedResult = system.TryPlaceOrder(consoleEnt, actor, orderId, targetNet, entries);
            duplicateResult = system.TryPlaceOrder(consoleEnt, actor, orderId, targetNet, entries);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(acceptedResult, Is.True);
            Assert.That(duplicateResult, Is.False);
            Assert.That(ui.TryGetUiState<CentCommSupplyBoundUserInterfaceState>(
                console,
                CentCommSupplyUiKey.Key,
                out var state), Is.True);
            Assert.That(state!.StatusMessage, Is.EqualTo(loc.GetString("centcomm-supply-error-order-already-accepted")));
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
    public async Task PalletCollisionUsesCrateFootprint()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var ticker = server.System<GameTicker>();
        var stationSystem = server.System<StationSystem>();
        var mapSystem = server.System<MapSystem>();
        var accessSystem = server.System<AccessSystem>();
        var system = server.System<CentCommSupplyConsoleSystem>();
        var fixtures = server.System<FixtureSystem>();
        var transform = server.System<SharedTransformSystem>();
        var bus = entMan.EventBus;

        EntityUid console = default;
        EntityUid target = default;
        MapId devMapId = default;
        MapId consoleMapId = default;
        EntityUid actor = default;
        var targetNet = NetEntity.Invalid;
        Entity<CentCommSupplyConsoleComponent> consoleEnt = default;

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
            var consoleComp = entMan.GetComponent<CentCommSupplyConsoleComponent>(console);
            consoleEnt = (console, consoleComp);
            actor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            GrantCentralCommandAccess(entMan, accessSystem, actor);
            targetNet = entMan.GetNetEntity(target);
            bus.RaiseLocalEvent(console, new BoundUIOpenedEvent(CentCommSupplyUiKey.Key, console, actor));
            bus.RaiseLocalEvent(console, new CentCommSupplySelectStationMessage(targetNet, Guid.NewGuid()) { Actor = actor });
        });

        var pallets = new List<EntityUid>();
        await server.WaitAssertion(() =>
        {
            pallets = GetBuyPallets(entMan, stationSystem, target);
            Assert.That(pallets, Is.Not.Empty, "Loaded Dev map has no buy cargo pallets");
        });

        var obstacles = new List<EntityUid>();
        var entries = new List<CentCommSupplyOrderEntry>
        {
            new("FishCentCommSupplyMedicalBundle", 1),
        };

        bool CanOrder()
        {
            return system.CanPlaceOrder(consoleEnt, actor, Guid.NewGuid(), targetNet, entries, out _);
        }

        void ClearObstacles()
        {
            foreach (var obstacle in obstacles)
            {
                if (entMan.EntityExists(obstacle))
                    entMan.DeleteEntity(obstacle);
            }
            obstacles.Clear();
        }

        void BlockOtherPallets(EntityUid targetPallet)
        {
            foreach (var pallet in pallets)
            {
                if (pallet == targetPallet)
                    continue;

                var xform = entMan.GetComponent<TransformComponent>(pallet);
                obstacles.Add(entMan.SpawnEntity("CrateGeneric", xform.Coordinates));
            }
        }

        EntityUid AddObstacle(
            EntityUid pallet,
            Vector2 offset,
            IPhysShape shape,
            int layer,
            int mask,
            bool hard = true)
        {
            var xform = entMan.GetComponent<TransformComponent>(pallet);
            var obstacle = entMan.SpawnEntity("CrateGeneric", xform.Coordinates.Offset(offset));
            // Обновляем тело после замены формы, иначе удаление последней формы отключит коллизии.
            fixtures.DestroyFixture(obstacle, "fix1", updates: false);
            if (!fixtures.TryCreateFixture(obstacle, shape, "test", hard: hard, collisionLayer: layer, collisionMask: mask))
                throw new InvalidOperationException("Failed to create test fixture");
            obstacles.Add(obstacle);
            return obstacle;
        }

        var targetPallet = EntityUid.Invalid;
        await server.WaitPost(() =>
        {
            foreach (var candidate in pallets)
            {
                ClearObstacles();
                BlockOtherPallets(candidate);
                if (CanOrder())
                {
                    targetPallet = candidate;
                    break;
                }
            }
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(targetPallet.IsValid(), Is.True, "Could not find a free buy pallet");
        });

        var originalRotation = Angle.Zero;
        await server.WaitAssertion(() =>
        {
            originalRotation = entMan.GetComponent<TransformComponent>(targetPallet).LocalRotation;
        });

        await server.WaitPost(() =>
        {
            ClearObstacles();
            BlockOtherPallets(targetPallet);
        });
        await server.WaitAssertion(() =>
        {
            Assert.That(CanOrder(), Is.True, "Free pallet baseline must allow an order");
        });

        await server.WaitPost(() =>
        {
            ClearObstacles();
            BlockOtherPallets(targetPallet);
            AddObstacle(targetPallet, Vector2.Zero, new PhysShapeCircle(0.4f),
                (int) CollisionGroup.Impassable, 0);
        });
        await server.WaitAssertion(() =>
        {
            Assert.That(CanOrder(), Is.False, "Layer-only collision must block a pallet");
        });

        await server.WaitPost(() =>
        {
            ClearObstacles();
            BlockOtherPallets(targetPallet);
            AddObstacle(targetPallet, Vector2.Zero, new PhysShapeCircle(0.4f),
                0, (int) CollisionGroup.MachineLayer);
        });
        await server.WaitAssertion(() =>
        {
            Assert.That(CanOrder(), Is.False, "Mask-only collision must block a pallet");
        });

        await server.WaitPost(() =>
        {
            ClearObstacles();
            BlockOtherPallets(targetPallet);
            AddObstacle(targetPallet, Vector2.Zero, new PhysShapeCircle(0.4f),
                (int) CollisionGroup.DoorPassable, (int) CollisionGroup.DoorPassable);
        });
        await server.WaitAssertion(() =>
        {
            Assert.That(CanOrder(), Is.True, "Non-matching layer/mask must not block a pallet");
        });

        await server.WaitPost(() =>
        {
            ClearObstacles();
            BlockOtherPallets(targetPallet);
            AddObstacle(targetPallet, Vector2.Zero, new PhysShapeCircle(0.4f),
                (int) CollisionGroup.MachineLayer, (int) CollisionGroup.CrateMask, hard: false);
        });
        await server.WaitAssertion(() =>
        {
            Assert.That(CanOrder(), Is.True, "Soft fixture must not block a pallet");
        });

        await server.WaitPost(() =>
        {
            ClearObstacles();
            BlockOtherPallets(targetPallet);
            AddObstacle(targetPallet, Vector2.Zero, new PhysShapeCircle(0.4f), 0, 0);
        });
        await server.WaitAssertion(() =>
        {
            Assert.That(CanOrder(), Is.True, "Zero layer/mask fixture must not block a pallet");
        });

        await server.WaitPost(() =>
        {
            ClearObstacles();
            consoleEnt.Comp.CratePrototype = "ClosetTool";
            BlockOtherPallets(targetPallet);
            AddObstacle(targetPallet, new Vector2(0, 0.47f), new PhysShapeCircle(0.01f),
                (int) CollisionGroup.MachineLayer, (int) CollisionGroup.MachineMask);
        });
        await server.WaitAssertion(() =>
        {
            Assert.That(CanOrder(), Is.False, "Crate footprint beyond the pallet must block a pallet");
        });

        await server.WaitPost(() =>
        {
            ClearObstacles();
            BlockOtherPallets(targetPallet);
            transform.SetLocalRotation(targetPallet, Angle.FromDegrees(90));
            AddObstacle(targetPallet, new Vector2(0.47f, 0), new PhysShapeCircle(0.01f),
                (int) CollisionGroup.MachineLayer, (int) CollisionGroup.MachineMask);
        });
        await server.WaitAssertion(() =>
        {
            Assert.That(CanOrder(), Is.True, "A crate with noRot must keep its unrotated footprint");
        });

        await server.WaitPost(() =>
        {
            transform.SetLocalRotation(targetPallet, originalRotation);
            ClearObstacles();
            consoleEnt.Comp.CratePrototype = "GhostBox";
            BlockOtherPallets(targetPallet);
            var xform = entMan.GetComponent<TransformComponent>(targetPallet);
            obstacles.Add(entMan.SpawnEntity("CrateGeneric", xform.Coordinates));
        });
        await server.WaitAssertion(() =>
        {
            Assert.That(CanOrder(), Is.True, "Crate prototype without hard fixtures must not block a pallet");
        });

        await server.WaitPost(() =>
        {
            ClearObstacles();
            consoleEnt.Comp.CratePrototype = "CrateCommandSecure";

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
    public async Task StationSelectionAckIsPerUser()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        await server.WaitPost(() => server.CfgMan.SetCVar(Robust.Shared.CVars.NetPVS, true));
        var clientOptions = client.ClientOptions!;
        var observerOptions = new RobustIntegrationTest.ClientIntegrationOptions
        {
            ContentAssemblies = clientOptions.ContentAssemblies,
            ContentStart = clientOptions.ContentStart,
            LoadTestAssembly = clientOptions.LoadTestAssembly,
            Options = clientOptions.Options,
            BeforeStart = clientOptions.BeforeStart,
        };
        foreach (var (cvar, value) in clientOptions.CVarOverrides)
            observerOptions.CVarOverrides.Add(cvar, value);

        using var observer = new RobustIntegrationTest.ClientIntegrationInstance(observerOptions);
        await observer.Connect(server);

        async Task RunTicks(int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                await pair.RunTicksSync(1);
                await observer.WaitRunTicks(1);
            }
        }

        await RunTicks(20);
        var requesterSession = server.PlayerMan.GetSessionById(client.Session!.UserId);
        var observerSession = server.PlayerMan.GetSessionById(observer.Session!.UserId);
        var previousRequester = requesterSession.AttachedEntity;
        var previousObserver = observerSession.AttachedEntity;
        var mapSystem = server.System<MapSystem>();
        var stationSystem = server.System<StationSystem>();
        var accessSystem = server.System<AccessSystem>();
        var ui = server.System<UserInterfaceSystem>();
        EntityUid firstStation = default, secondStation = default;
        EntityUid requester = default, spectator = default, console = default;
        NetEntity firstNet = default, secondNet = default, consoleNet = default;
        MapId firstMap = default, secondMap = default, consoleMap = default;
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var stationConfig = server.ProtoMan.Index<GameMapPrototype>(MapProto).Stations.Values.Single();
            var plating = server.ResolveDependency<ITileDefinitionManager>()["Plating"];

            EntityUid CreateStation(out MapId mapId)
            {
                mapSystem.CreateMap(out mapId);
                var grid = mapSystem.CreateGridEntity(mapId);
                var coords = new EntityCoordinates(grid, 0.5f, 0.5f);
                mapSystem.SetTile(grid, coords, new Tile(plating.TileId));
                entMan.SpawnEntity("CargoPalletBuy", coords);
                return stationSystem.InitializeNewStation(stationConfig, [grid.Owner]);
            }

            firstStation = CreateStation(out firstMap);
            secondStation = CreateStation(out secondMap);
            firstNet = entMan.GetNetEntity(firstStation);
            secondNet = entMan.GetNetEntity(secondStation);

            mapSystem.CreateMap(out consoleMap);
            var consoleCoords = new MapCoordinates(0, 0, consoleMap);
            requester = entMan.SpawnEntity(null, consoleCoords);
            spectator = entMan.SpawnEntity(null, consoleCoords);
            entMan.EnsureComponent<EyeComponent>(requester);
            entMan.EnsureComponent<EyeComponent>(spectator);
            entMan.EnsureComponent<ComplexInteractionComponent>(requester);
            entMan.EnsureComponent<ComplexInteractionComponent>(spectator);
            GrantCentralCommandAccess(entMan, accessSystem, requester);
            GrantCentralCommandAccess(entMan, accessSystem, spectator);
            server.PlayerMan.SetAttachedEntity(requesterSession, requester);
            server.PlayerMan.SetAttachedEntity(observerSession, spectator);

            console = entMan.SpawnEntity("FishCentCommSupplyConsole", consoleCoords);
            consoleNet = entMan.GetNetEntity(console);
            var consoleComp = entMan.GetComponent<CentCommSupplyConsoleComponent>(console);
            consoleComp.SelectedStation = firstStation;
        });

        // Сначала синхронизируем привязку игроков, затем открываем интерфейс для обоих.
        await RunTicks(20);
        await server.WaitPost(() =>
        {
            ui.OpenUi(console, CentCommSupplyUiKey.Key, requester);
            ui.OpenUi(console, CentCommSupplyUiKey.Key, spectator);
        });

        await RunTicks(20);
        await server.WaitAssertion(() => Assert.That(
            ui.GetActors(console, CentCommSupplyUiKey.Key), Is.EquivalentTo(new[] { requester, spectator })));

        CentCommSupplyWindow requesterWindow = null!;
        CentCommSupplyWindow observerWindow = null!;
        await client.WaitAssertion(() =>
        {
            var clientConsole = client.EntMan.GetEntity(consoleNet);
            Assert.That(client.System<Robust.Client.GameObjects.UserInterfaceSystem>()
                .TryGetOpenUi<CentCommSupplyBoundUserInterface>(clientConsole, CentCommSupplyUiKey.Key, out var bui), Is.True);
            requesterWindow = GetPrivateField<CentCommSupplyWindow>(bui!, "_window");
            Assert.That(GetPrivateField<NetEntity?>(requesterWindow, "_selectedStation"), Is.EqualTo(firstNet));
        });
        await observer.WaitAssertion(() =>
        {
            var clientConsole = observer.EntMan.GetEntity(consoleNet);
            Assert.That(observer.System<Robust.Client.GameObjects.UserInterfaceSystem>()
                .TryGetOpenUi<CentCommSupplyBoundUserInterface>(clientConsole, CentCommSupplyUiKey.Key, out var bui), Is.True);
            observerWindow = GetPrivateField<CentCommSupplyWindow>(bui!, "_window");
        });

        var observerReceivedResult = false;
        await observer.WaitPost(() =>
        {
            // Проверяем входящие сообщения до того, как окно отфильтрует чужой SelectionId.
            observer.ResolveDependency<IEntityNetworkManager>().ReceivedSystemMessage += (_, message) =>
            {
                if (message.GetType().Name == "BoundUIWrapMessage" &&
                    message.GetType().GetField("Message")?.GetValue(message) is CentCommSupplyStationSelectionResultMessage)
                {
                    observerReceivedResult = true;
                }
            };
        });

        await client.WaitPost(() =>
        {
            GetPrivateField<Dictionary<string, int>>(requesterWindow, "_order").Add("FishCentCommSupplyMedicalBundle", 1);
            requesterWindow.SelectStation(secondNet);
        });
        await RunTicks(20);

        await client.WaitAssertion(() =>
        {
            Assert.That(GetPrivateField<bool>(requesterWindow, "_stationSelectionPending"), Is.False);
            Assert.That(GetPrivateField<NetEntity?>(requesterWindow, "_selectedStation"), Is.EqualTo(secondNet));
            Assert.That(GetPrivateField<Dictionary<string, int>>(requesterWindow, "_order"), Is.Empty);
        });
        await observer.WaitAssertion(() =>
        {
            Assert.That(GetPrivateField<NetEntity?>(observerWindow, "_selectedStation"), Is.EqualTo(secondNet));
            Assert.That(observerReceivedResult, Is.False, "A station selection result reached another viewer");
        });

        await server.WaitPost(() =>
        {
            ui.CloseUi(console, CentCommSupplyUiKey.Key);
            server.PlayerMan.SetAttachedEntity(requesterSession, previousRequester);
            server.PlayerMan.SetAttachedEntity(observerSession, previousObserver);
            server.EntMan.DeleteEntity(firstStation);
            server.EntMan.DeleteEntity(secondStation);
            mapSystem.DeleteMap(firstMap);
            mapSystem.DeleteMap(secondMap);
            mapSystem.DeleteMap(consoleMap);
        });
        await observer.WaitPost(() => observer.ResolveDependency<IClientNetManager>().ClientDisconnect("Test finished"));
        await RunTicks(10);
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StationSelectionIgnoresUnrelatedUpdates()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        CentCommSupplyWindow window = null!;
        var firstStation = new NetEntity(1);
        var secondStation = new NetEntity(2);
        var selectionId = Guid.Empty;

        await client.WaitPost(() =>
        {
            window = new CentCommSupplyWindow();
            window.OnStationSelected += (_, id) =>
            {
                selectionId = id;
                return true;
            };

            var state = new CentCommSupplyBoundUserInterfaceState(
                new List<CentCommSupplyProductEntry>(),
                new Dictionary<NetEntity, string>
                {
                    [firstStation] = "First",
                    [secondStation] = "Second",
                },
                null,
                6,
                8,
                null,
                false);
            window.UpdateState(state);
            window.SelectStation(secondStation);
        });

        await client.WaitAssertion(() =>
        {
            Assert.That(selectionId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(GetPrivateField<bool>(window!, "_stationSelectionPending"), Is.True);
            Assert.That(GetPrivateField<NetEntity?>(window!, "_selectedStation"), Is.EqualTo(secondStation));
        });

        await client.WaitPost(() =>
        {
            var state = new CentCommSupplyBoundUserInterfaceState(
                new List<CentCommSupplyProductEntry>(),
                new Dictionary<NetEntity, string>
                {
                    [firstStation] = "First",
                    [secondStation] = "Second",
                },
                firstStation,
                6,
                8,
                null,
                false);
            window!.UpdateState(state);
        });

        await client.WaitAssertion(() =>
        {
            Assert.That(GetPrivateField<bool>(window!, "_stationSelectionPending"), Is.True,
                "Another user's shared state cleared the pending selection");
            Assert.That(GetPrivateField<NetEntity?>(window!, "_selectedStation"), Is.EqualTo(secondStation));
        });

        await client.WaitPost(() =>
        {
            window!.OnStationSelectionResult(Guid.NewGuid(), true, firstStation, null);
        });

        await client.WaitAssertion(() =>
        {
            Assert.That(GetPrivateField<bool>(window!, "_stationSelectionPending"), Is.True,
                "A stale acknowledgement cleared the pending selection");
        });

        await client.WaitPost(() =>
        {
            window!.OnStationSelectionResult(selectionId, false, null, "rejected");
        });

        await client.WaitAssertion(() =>
        {
            Assert.That(GetPrivateField<bool>(window!, "_stationSelectionPending"), Is.False);
            Assert.That(GetPrivateField<NetEntity?>(window!, "_selectedStation"), Is.EqualTo(firstStation));
        });

        await client.WaitPost(() =>
        {
            window?.Dispose();
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Grants CentralCommand access to the test actor.
    /// </summary>
    private static void GrantCentralCommandAccess(IEntityManager entMan, AccessSystem accessSystem, EntityUid actor)
    {
        var access = entMan.AddComponent<AccessComponent>(actor);
        accessSystem.TrySetTags(actor,
            new[] { new ProtoId<AccessLevelPrototype>("CentralCommand") },
            access);
    }

    /// <summary>
    /// Returns crates that were not present before the order.
    /// </summary>
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

    /// <summary>
    /// Returns anchored buy cargo pallets owned by the station.
    /// </summary>
    private static List<EntityUid> GetBuyPallets(
        IEntityManager entMan,
        StationSystem stationSystem,
        EntityUid station)
    {
        var pallets = new List<EntityUid>();
        var query = entMan.AllEntityQueryEnumerator<CargoPalletComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var pallet, out var xform))
        {
            if (!xform.Anchored
                || (pallet.PalletType & BuySellType.Buy) == 0
                || stationSystem.GetOwningStation(uid) != station)
            {
                continue;
            }

            pallets.Add(uid);
        }

        return pallets;
    }

    private static int SecondsToTicks(TimeSpan tickPeriod, double seconds)
    {
        return Math.Max(1, (int) Math.Ceiling(seconds / tickPeriod.TotalSeconds));
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field {fieldName} was not found on {instance.GetType().Name}");
        return (T) field.GetValue(instance)!;
    }
}
