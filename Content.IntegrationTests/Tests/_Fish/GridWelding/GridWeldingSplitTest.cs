using System.Collections.Generic;
using System.Linq;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Robust.Server.Physics;
using Robust.Shared;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Fish.GridWelding;

[TestFixture]
public sealed class GridWeldingSplitTest
{
    [TestCase("cargo", true)]
    [TestCase("emergency", true)]
    [TestCase("cargo", false)]
    public async Task LoadedShuttleCanBeCutAfterWelding(string name, bool splitEnabled)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var entities = server.EntMan;
        var previousSplitting = server.CfgMan.GetCVar(CVars.GridSplitting);
        EntityUid map = default;
        EntityUid first = default;
        EntityUid donor = default;
        EntityUid marker = default;
        Vector2i bridge = default;

        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(CVars.GridSplitting, splitEnabled);
            var maps = entities.System<SharedMapSystem>();
            map = maps.CreateMap(out var mapId);
            var loaded = entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/Shuttles/{name}.yml"), out var shuttle);
            if (!loaded)
                throw new System.InvalidOperationException("Could not load the test shuttle.");

            first = shuttle.Value.Owner;
            var grid = shuttle.Value.Comp;
            var edge = maps.GetAllTiles(first, grid).MaxBy(t => t.GridIndices.X).GridIndices;
            bridge = edge + Vector2i.Right;
            var tile = new Tile(server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            maps.SetTile(first, grid, bridge, tile);
            var b = server.ResolveDependency<IMapManager>().CreateGridEntity(mapId);
            donor = b.Owner;
            maps.SetTiles(b, [(bridge + Vector2i.Right, tile), (bridge + Vector2i.Right * 2, tile)]);
            var transform = entities.System<SharedTransformSystem>();
            var xform = entities.GetComponent<TransformComponent>(first);
            var (position, rotation) = transform.GetWorldPositionRotation(xform);
            transform.SetWorldPositionRotation(donor, position, rotation);
            marker = entities.SpawnEntity("WallSolid", maps.GridTileToLocal(donor, b.Comp, bridge + Vector2i.Right * 2));
            var userCoordinates = maps.GridTileToLocal(first, grid, bridge);
            var user = entities.SpawnEntity("MobHuman", userCoordinates);
            var welder = entities.SpawnEntity("Welder", userCoordinates);
            entities.System<SharedHandsSystem>().TryPickupAnyHand(user, welder);
            entities.System<ItemToggleSystem>().TryActivate(welder, user);
            var interaction = entities.System<SharedInteractionSystem>();
            interaction.UserInteraction(user, userCoordinates, null);
            interaction.UserInteraction(user, maps.GridTileToLocal(donor, b.Comp, bridge + Vector2i.Right), null);
        });
        await pair.RunSeconds(6);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(donor), Is.False);
            Assert.That(entities.GetComponent<TransformComponent>(marker).GridUid, Is.EqualTo(first));
        });
        await server.WaitPost(() =>
        {
            var maps = entities.System<SharedMapSystem>();
            var grid = entities.GetComponent<MapGridComponent>(first);
            maps.SetTile(first, grid, maps.GridTileToLocal(first, grid, bridge), Tile.Empty);
        });
        await pair.RunSeconds(1);
        await server.WaitAssertion(() =>
        {
            var parent = entities.GetComponent<TransformComponent>(marker).GridUid;
            Assert.That(parent == first, Is.EqualTo(!splitEnabled),
                "Only the global grid-splitting setting may keep the cut fragment on the shuttle.");
        });
        if (!splitEnabled)
        {
            await server.WaitPost(() =>
            {
                server.CfgMan.SetCVar(CVars.GridSplitting, true);
                entities.System<GridFixtureSystem>().CheckSplits(first);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(entities.GetComponent<TransformComponent>(marker).GridUid, Is.Not.EqualTo(first));
            });
        }
        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(CVars.GridSplitting, previousSplitting);
            entities.DeleteEntity(map);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(0)]
    [TestCase(12)]
    [TestCase(13)]
    [TestCase(-4)]
    public async Task RemovingWeldedBridgeSeparatesTheShuttleAgain(int baseX)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var entities = server.EntMan;
        var splitting = server.CfgMan.GetCVar(CVars.GridSplitting);
        EntityUid map = default;
        EntityUid first = default;
        EntityUid second = default;
        EntityUid machine = default;
        EntityUid user = default;
        var bridge = new Vector2i(baseX + 3, 1);
        Tile plating = default;

        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(CVars.GridSplitting, true);
            var maps = entities.System<SharedMapSystem>();
            map = maps.CreateMap(out var mapId);
            var manager = server.ResolveDependency<IMapManager>();
            var a = manager.CreateGridEntity(mapId);
            var b = manager.CreateGridEntity(mapId);
            first = a.Owner;
            second = b.Owner;
            plating = new Tile(server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            var tilesA = new List<(Vector2i, Tile)>();
            var tilesB = new List<(Vector2i, Tile)>();
            for (var x = 0; x < 3; x++)
            {
                for (var y = 0; y < 3; y++)
                    tilesA.Add((new Vector2i(baseX + x, y), plating));
            }
            tilesA.Add((bridge, plating));
            for (var x = 4; x < 6; x++)
            {
                for (var y = 0; y < 3; y++)
                    tilesB.Add((new Vector2i(baseX + x, y), plating));
            }
            maps.SetTiles(a, tilesA);
            maps.SetTiles(b, tilesB);
            machine = entities.SpawnEntity("WallSolid", new EntityCoordinates(second, baseX + 5.5f, 2.5f));
            user = entities.SpawnEntity("MobHuman", new EntityCoordinates(first, baseX + 2.5f, 1.5f));
            var welder = entities.SpawnEntity("Welder", new EntityCoordinates(first, baseX + 2.5f, 1.5f));
            entities.System<SharedHandsSystem>().TryPickupAnyHand(user, welder);
            entities.System<ItemToggleSystem>().TryActivate(welder, user);
        });

        // Повторяем сварку и удаление шва, в том числе на границе чанков.
        for (var cycle = 0; cycle < 3; cycle++)
        {
            var donor = second;
            await server.WaitPost(() =>
            {
                var maps = entities.System<SharedMapSystem>();
                maps.SetTile(first, entities.GetComponent<MapGridComponent>(first), bridge, plating);
                var interaction = entities.System<SharedInteractionSystem>();
                interaction.UserInteraction(user, new EntityCoordinates(first, baseX + 3.5f, 1.5f), null);
                interaction.UserInteraction(user, new EntityCoordinates(donor, baseX + 4.5f, 1.5f), null);
            });
            await pair.RunSeconds(6);
            await server.WaitAssertion(() =>
            {
                Assert.That(entities.EntityExists(donor), Is.False, "Welding must first consume the other grid.");
                Assert.That(entities.GetComponent<TransformComponent>(machine).GridUid, Is.EqualTo(first));
            });

            await server.WaitPost(() =>
            {
                // Админское размещение пустоты вызывает тот же SetTile с EntityCoordinates.
                var maps = entities.System<SharedMapSystem>();
                maps.SetTile(first, entities.GetComponent<MapGridComponent>(first),
                    new EntityCoordinates(first, baseX + 3.5f, 1.5f), Tile.Empty);
                second = entities.GetComponent<TransformComponent>(machine).GridUid ?? default;
            });
            await pair.RunSeconds(1);
            await server.WaitAssertion(() =>
            {
                Assert.That(second, Is.Not.EqualTo(first), "Removing the only connecting tile must split the welded grid.");
                Assert.That(entities.EntityExists(second), Is.True);
                Assert.That(entities.GetComponent<TransformComponent>(machine).Anchored, Is.True);
                var manager = server.ResolveDependency<IMapManager>();
                Assert.That(manager.GetAllGrids(entities.GetComponent<TransformComponent>(first).MapID).Count(), Is.EqualTo(2));
            });
        }

        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(CVars.GridSplitting, splitting);
            entities.DeleteEntity(map);
        });
        await pair.CleanReturnAsync();
    }
}
