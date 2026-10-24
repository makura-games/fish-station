using System.Numerics;
using Content.Server._Fish.GridWelding;
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
    [TestCase("item-target", true)]
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
            var manager = server.ResolveDependency<IMapManager>();
            map = maps.CreateMap(out var mapId);
            var first = manager.CreateGridEntity(mapId);
            var second = manager.CreateGridEntity(mapId);
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
                Assert.That(manager.TryFindGridAt(new MapCoordinates(0.75f, 0.5f, mapId), out var lookup, out _));
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
}
