using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server._Fish.CentCommSupply;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared._Fish.CentCommSupply;
using Content.Shared.Access.Components;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.Paper;
using Content.Shared.Storage.Components;
using NUnit.Framework;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Fish.CentCommSupply;

[TestFixture]
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
        EntityUid actor = default;
        var targetNet = NetEntity.Invalid;
        var existingCrates = new HashSet<EntityUid>();

        await server.WaitPost(() =>
        {
            var map = protoMan.Index<GameMapPrototype>(MapProto);
            ticker.LoadGameMap(map, out _, DeserializationOptions.Default with { InitializeMaps = true });
        });

        await server.WaitAssertion(() =>
        {
            var stations = stationSystem.GetStations();
            target = stations.Single(station =>
                entMan.GetComponent<MetaDataComponent>(station).EntityName == "Dev");
        });

        await server.WaitPost(() =>
        {
            var consoleMap = mapSystem.CreateMap(out var consoleMapId);
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

            Assert.That(products, Does.Contain("ClothingBackpackDuffelNanotrasenMedicalBundleFilled"));
            Assert.That(products, Does.Contain("FishClothingBackpackDuffelWeaponAsh12BlueShield"));
            Assert.That(products, Does.Contain("MagazineAsh12Slug"));
            Assert.That(products, Does.Contain("BlueprintMagAsh12Basic"));
        });

        await pair.CleanReturnAsync();
    }
}
