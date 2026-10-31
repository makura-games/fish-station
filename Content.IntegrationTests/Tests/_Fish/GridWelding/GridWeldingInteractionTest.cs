using System.Numerics;
using Content.IntegrationTests.Tests.Helpers;
using Content.Server._Fish.GridWelding;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests._Fish.GridWelding;

[TestFixture]
public sealed class GridWeldingInteractionTest
{
    private sealed class InteractUsingListenerSystem : TestListenerSystem<InteractUsingEvent>;

    /// <summary>
    /// Resolves tile and grid clicks, including overlapping grids, without consuming ordinary item interactions.
    /// </summary>
    [TestCase("item-target", false)]
    [TestCase("grid-target", true)]
    [TestCase("overlapping-grids", true)]
    [TestCase("overlapping-world-pick", true)]
    [TestCase("edge-in-reach", true)]
    [TestCase("blocked-edge", false)]
    [TestCase("same-grid-item", false)]
    [TestCase("ordinary-item-use", false)]
    public async Task SecondClickSelectsTheIntendedGrid(string scenario, bool startsWelding)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var entities = server.EntMan;
        EntityUid map = default;
        EntityUid welder = default;
        EntityUid user = default;
        EntityUid selected = default;
        Vector2i selectedTile = default;

        await server.WaitPost(() =>
        {
            var maps = entities.System<SharedMapSystem>();
            var transform = entities.System<SharedTransformSystem>();
            map = maps.CreateMap(out var mapId);
            var first = maps.CreateGridEntity(mapId);
            var second = maps.CreateGridEntity(mapId);
            var tile = new Tile(server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            maps.SetTiles(first, [(new Vector2i(0, 0), tile), (new Vector2i(0, 1), tile)]);
            maps.SetTiles(second, [(new Vector2i(1, 0), tile), (new Vector2i(1, 1), tile)]);

            var firstClick = new EntityCoordinates(first, 0.5f, 0.5f);
            var secondClick = new EntityCoordinates(second, 1.5f, 0.5f);
            selected = first.Owner;
            EntityUid? target = null;
            if (scenario is "edge-in-reach" or "blocked-edge")
            {
                transform.SetWorldPosition(second, new Vector2(1, 0));
                secondClick = new EntityCoordinates(second, 1.05f, 0.5f);
            }
            if (scenario is "overlapping-grids" or "overlapping-world-pick")
            {
                transform.SetWorldPosition(second, new Vector2(-0.75f, 0));
                Assert.That(maps.TryFindGridAt(new MapCoordinates(0.75f, 0.5f, mapId), out var lookup, out _));
                // Проверяем оба возможных порядка обхода дерева гридов.
                selected = lookup;
                firstClick = lookup == first.Owner
                    ? new EntityCoordinates(first, 0.1f, 0.5f)
                    : new EntityCoordinates(second, 1.9f, 0.5f);
                var secondReference = scenario == "overlapping-world-pick"
                    ? lookup
                    : lookup == first.Owner ? second.Owner : first.Owner;
                secondClick = transform.WithEntityId(new EntityCoordinates(map, 0.75f, 0.5f), secondReference);
            }
            if (scenario == "grid-target")
                target = second.Owner;
            if (scenario is "item-target" or "ordinary-item-use")
                target = entities.SpawnEntity("Crowbar", secondClick);
            if (scenario == "same-grid-item")
            {
                secondClick = new EntityCoordinates(first, 0.5f, 1.5f);
                target = entities.SpawnEntity("Crowbar", secondClick);
            }
            if (scenario == "blocked-edge")
                entities.SpawnEntity("WallSolid", new EntityCoordinates(map, 1.5f, 0.5f));

            var physics = entities.System<SharedPhysicsSystem>();
            physics.SetLinearVelocity(first, new Vector2(2, 0));
            physics.SetLinearVelocity(second, new Vector2(2, 0));
            user = entities.SpawnEntity("MobHuman", new EntityCoordinates(first, 0.9f, 0.5f));
            welder = entities.SpawnEntity("Welder", new EntityCoordinates(first, 0.5f, 0.5f));
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, welder));
            Assert.That(entities.System<ItemToggleSystem>().TryActivate(welder, user));

            var interaction = entities.System<SharedInteractionSystem>();
            selectedTile = firstClick.Position.Floored();
            if (scenario != "ordinary-item-use")
                interaction.UserInteraction(user, firstClick, null);
            interaction.UserInteraction(user, secondClick, target);
        });

        await server.WaitAssertion(() =>
        {
            var state = entities.GetComponent<GridWeldingComponent>(welder);
            Assert.That(state.DoAfter != null, Is.EqualTo(startsWelding));
            if (scenario != "ordinary-item-use")
            {
                Assert.That(state.Grid, Is.EqualTo(selected), "The second click must not replace the first selected grid.");
                Assert.That(state.Tile, Is.EqualTo(selectedTile));
            }
            else
            {
                var selectedGrid = state.Grid;
                Assert.That(selectedGrid.IsValid(), Is.False, "Ordinary item interactions must not start grid selection.");
            }
        });

        await server.WaitPost(() =>
        {
            entities.System<SharedHandsSystem>().TryDrop(user, welder);
            entities.DeleteEntity(map);
        });
        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// After selecting an edge, an entity on the other grid must receive ordinary interactions while tile/grid clicks
    /// can still start welding. The public selection API follows the same rule, including during an active DoAfter.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public async Task OrdinaryEntityInteractionPreservesSelection(bool targetGrid)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var entities = server.EntMan;
        EntityUid map = default;
        EntityUid first = default;
        EntityUid second = default;
        EntityUid target = default;
        EntityUid user = default;
        EntityUid welder = default;
        EntityCoordinates firstClick = default;
        EntityCoordinates secondClick = default;
        var pickedUp = false;
        var activated = false;
        var acceptedEntity = true;
        DoAfterId? welding = null;

        await server.WaitPost(() =>
        {
            var maps = entities.System<SharedMapSystem>();
            map = maps.CreateMap(out var mapId);
            var a = maps.CreateGridEntity(mapId);
            var b = maps.CreateGridEntity(mapId);
            first = a.Owner;
            second = b.Owner;
            var tile = new Tile(server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            maps.SetTiles(a, [(new Vector2i(0, 0), tile), (new Vector2i(0, 1), tile)]);
            maps.SetTiles(b, [(new Vector2i(1, 0), tile), (new Vector2i(1, 1), tile)]);
            firstClick = new EntityCoordinates(first, 0.5f, 0.5f);
            secondClick = new EntityCoordinates(second, 1.5f, 0.5f);
            target = entities.SpawnEntity("Crowbar", secondClick);
            entities.AddComponent<TestListenerComponent>(target);
            user = entities.SpawnEntity("MobHuman", firstClick);
            welder = entities.SpawnEntity("Welder", firstClick);
            pickedUp = entities.System<SharedHandsSystem>().TryPickupAnyHand(user, welder);
            activated = entities.System<ItemToggleSystem>().TryActivate(welder, user);
            entities.System<SharedInteractionSystem>().UserInteraction(user, firstClick, null);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(pickedUp, Is.True);
            Assert.That(activated, Is.True);
            var selection = entities.GetComponent<GridWeldingComponent>(welder);
            Assert.That(selection.User, Is.EqualTo(user));
            Assert.That(selection.Grid, Is.EqualTo(first));
            Assert.That(selection.Tile, Is.EqualTo(Vector2i.Zero));
            Assert.That(selection.DoAfter, Is.Null);
            Assert.That(entities.GetComponent<TransformComponent>(target).GridUid, Is.EqualTo(second));
        });

        await server.WaitPost(() =>
        {
            // API проверяем отдельно: фильтр только в обработчике события не защищает прямые вызовы.
            acceptedEntity = entities.System<GridWeldingSystem>().TrySelectTile((welder, null), user, secondClick, target);
            entities.System<SharedInteractionSystem>().UserInteraction(user, secondClick, target);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(acceptedEntity, Is.False);
            Assert.That(entities.System<InteractUsingListenerSystem>().Count(target), Is.EqualTo(1),
                "The target on the other grid must receive the ordinary welder interaction.");
            var selection = entities.GetComponent<GridWeldingComponent>(welder);
            Assert.That(selection.User, Is.EqualTo(user));
            Assert.That(selection.Grid, Is.EqualTo(first));
            Assert.That(selection.Tile, Is.EqualTo(Vector2i.Zero));
            Assert.That(selection.DoAfter, Is.Null);
        });

        await server.WaitPost(() =>
        {
            entities.System<SharedInteractionSystem>().UserInteraction(user, secondClick, targetGrid ? second : null);
            welding = entities.GetComponent<GridWeldingComponent>(welder).DoAfter;
        });
        await server.WaitAssertion(() => Assert.That(welding, Is.Not.Null,
            "Selecting the tile or grid after an ordinary entity interaction must still start welding."));

        await server.WaitPost(() =>
        {
            acceptedEntity = entities.System<GridWeldingSystem>().TrySelectTile((welder, null), user, secondClick, target);
            entities.System<SharedInteractionSystem>().UserInteraction(user, secondClick, target);
        });
        await server.WaitAssertion(() =>
        {
            Assert.That(acceptedEntity, Is.False);
            Assert.That(entities.System<InteractUsingListenerSystem>().Count(target), Is.EqualTo(2),
                "An active welding DoAfter must not consume ordinary entity interactions either.");
            var selection = entities.GetComponent<GridWeldingComponent>(welder);
            Assert.That(selection.User, Is.EqualTo(user));
            Assert.That(selection.Grid, Is.EqualTo(first));
            Assert.That(selection.Tile, Is.EqualTo(Vector2i.Zero));
            Assert.That(selection.DoAfter, Is.EqualTo(welding));
        });

        await server.WaitPost(() =>
        {
            entities.System<SharedHandsSystem>().TryDrop(user, welder);
            entities.DeleteEntity(map);
        });
        await pair.CleanReturnAsync();
    }
}
