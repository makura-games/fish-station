using System.Numerics;
using System.Linq;
using Content.Server._Fish.GridWelding;
using Content.Server._Sunrise.ImmortalGrid;
using Content.Server.Decals;
using Content.Server.Power.EntitySystems;
using Content.Shared._Sunrise.UnbuildableGrid;
using Content.Shared.Gravity;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Tiles;
using Robust.Shared.GameObjects;
using Robust.Shared;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests._Fish.GridWelding;

[TestFixture]
public sealed class GridWeldingTest
{
    [TestCase("moving", true)]
    [TestCase("larger-second", true)]
    [TestCase("quarter-turn", true)]
    [TestCase("lattice-survivor", true)]
    [TestCase("one-tile-gap", true)]
    [TestCase("slight-angle", true)]
    [TestCase("offset-and-angle", true)]
    [TestCase("gap-too-wide", false)]
    [TestCase("large-rotation-snap", true)]
    [TestCase("large-first-rotation-snap", true)]
    [TestCase("large-second-alignment", true)]
    [TestCase("unlit", false)]
    [TestCase("overlap", false)]
    [TestCase("wide-angle", true)]
    [TestCase("diagonal-angle", true)]
    [TestCase("fast-separation", false)]
    [TestCase("drift-during-welding", false)]
    [TestCase("dropped-welder", false)]
    [TestCase("removed-tile", false)]
    [TestCase("unreachable", false)]
    [TestCase("protected", false)]
    [TestCase("unbuildable", false)]
    [TestCase("immortal", false)]
    [TestCase("shared-spin", false)]
    public async Task TileClicksValidateAndJoinMovingGrids(string scenario, bool succeeds)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var entities = server.EntMan;
        EntityUid first = default;
        EntityUid second = default;
        EntityUid item = default;
        EntityUid map = default;
        EntityUid survivor = default;
        EntityUid consumed = default;
        EntityUid user = default;
        EntityUid welder = default;

        await server.WaitPost(() =>
        {
            var maps = entities.System<SharedMapSystem>();
            map = maps.CreateMap(out var mapId);
            var manager = server.ResolveDependency<IMapManager>();
            var a = manager.CreateGridEntity(mapId);
            var b = manager.CreateGridEntity(mapId);
            first = a.Owner;
            second = b.Owner;
            var tile = new Tile(server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            var firstTile = scenario == "quarter-turn" ? new Vector2i(1, -1) : Vector2i.Zero;
            if (scenario == "quarter-turn")
                maps.SetTiles(a, [(new Vector2i(1, -1), tile), (new Vector2i(0, -1), tile)]);
            else
                maps.SetTiles(a, [(new Vector2i(0, 0), tile), (new Vector2i(0, 1), tile)]);
            var hasGap = scenario is "one-tile-gap" or "gap-too-wide" or "offset-and-angle";
            if (hasGap)
                maps.SetTiles(a, [(new Vector2i(0, -1), tile), (new Vector2i(1, -1), tile)]);
            if (scenario == "lattice-survivor")
            {
                var lattice = new Tile(server.ResolveDependency<ITileDefinitionManager>()["Lattice"].TileId);
                maps.SetTiles(a, [(new Vector2i(0, 0), lattice), (new Vector2i(0, 1), lattice), (new Vector2i(0, 2), lattice)]);
            }
            if (scenario == "quarter-turn")
                maps.SetTiles(b, [(new Vector2i(0, 0), tile), (new Vector2i(0, -1), tile)]);
            else
                maps.SetTiles(b, [(new Vector2i(1, 0), tile), (new Vector2i(1, 1), tile)]);

            var transform = entities.System<SharedTransformSystem>();
            var secondTile = new Vector2i(1, 0);
            survivor = first;
            consumed = second;
            if (scenario == "larger-second")
            {
                maps.SetTile(b, new Vector2i(1, 2), tile);
                survivor = second;
                consumed = first;
            }
            if (scenario == "overlap")
                maps.SetTile(b, new Vector2i(0, 1), tile);
            if (scenario == "quarter-turn")
            {
                transform.SetWorldPositionRotation(second, new Vector2(2, 0), Angle.FromDegrees(90));
                secondTile = Vector2i.Zero;
            }
            if (scenario == "one-tile-gap")
                transform.SetWorldPosition(second, new Vector2(1, 0));
            if (scenario == "gap-too-wide")
                transform.SetWorldPosition(second, new Vector2(1.1f, 0));
            if (scenario == "slight-angle")
                transform.SetWorldRotation(second, Angle.FromDegrees(10));
            if (scenario == "offset-and-angle")
                transform.SetWorldPositionRotation(second, new Vector2(0.6f, 0.25f), Angle.FromDegrees(6));
            if (scenario is "large-rotation-snap" or "large-second-alignment")
            {
                for (var y = 2; y <= 10; y++)
                    maps.SetTile(b, new Vector2i(1, y), tile);
                if (scenario == "large-second-alignment")
                {
                    survivor = second;
                    consumed = first;
                }
            }
            if (scenario == "large-first-rotation-snap")
            {
                for (var y = 2; y <= 10; y++)
                    maps.SetTile(a, new Vector2i(0, y), tile);
                survivor = second;
                consumed = first;
            }
            if (scenario is "wide-angle" or "diagonal-angle" or "large-rotation-snap" or
                "large-first-rotation-snap" or "large-second-alignment")
            {
                var rotation = Angle.FromDegrees(scenario == "diagonal-angle" ? -45 : -35);
                var position = new Vector2(1.9f, 0.5f) - rotation.RotateVec(new Vector2(1.5f, 0.5f));
                transform.SetWorldPositionRotation(second, position, rotation);
            }
            if (scenario == "unreachable")
                transform.SetWorldPosition(second, new Vector2(10, 0));
            if (scenario == "protected")
                entities.EnsureComponent<ProtectedGridComponent>(second);
            if (scenario == "unbuildable")
                entities.EnsureComponent<UnbuildableGridComponent>(second);
            if (scenario == "immortal")
                entities.EnsureComponent<ImmortalGridComponent>(second);

            var physics = entities.System<SharedPhysicsSystem>();
            physics.SetBodyType(first, BodyType.Dynamic);
            physics.SetBodyType(second, BodyType.Dynamic);
            physics.SetLinearVelocity(first, new Vector2(2, 0));
            physics.SetLinearVelocity(second, new Vector2(2, 0));
            if (scenario == "large-rotation-snap")
            {
                physics.SetBodyType(first, BodyType.Static);
                physics.SetLinearVelocity(second, Vector2.Zero);
            }
            if (scenario == "large-first-rotation-snap")
            {
                physics.SetBodyType(second, BodyType.Static);
                physics.SetLinearVelocity(first, Vector2.Zero);
            }
            if (scenario == "fast-separation")
                physics.SetLinearVelocity(second, new Vector2(4, 0));
            if (scenario == "shared-spin")
            {
                physics.SetLinearVelocity(first, new Vector2(2, -0.2f));
                physics.SetLinearVelocity(second, new Vector2(2, 0.2f));
                physics.SetAngularVelocity(first, 0.4f);
                physics.SetAngularVelocity(second, 0.4f);
            }

            var userPosition = hasGap
                ? new Vector2(1.3f, -0.15f)
                : (Vector2) firstTile + new Vector2(0.5f);
            user = entities.SpawnEntity("MobHuman", new EntityCoordinates(first, userPosition));
            welder = entities.SpawnEntity("Welder", new EntityCoordinates(first, (Vector2) firstTile + new Vector2(0.5f)));
            var itemCoordinates = scenario == "quarter-turn"
                ? new EntityCoordinates(second, 0.5f, -0.5f)
                : new EntityCoordinates(second, 1.5f, 1.5f);
            item = entities.SpawnEntity("Crowbar", itemCoordinates);
            if (scenario == "quarter-turn")
            {
                Assert.That(entities.System<DecalSystem>().TryAddDecal("Dirt", new EntityCoordinates(second, 0, 0.8f), out _));
            }
            if (scenario == "lattice-survivor")
                Assert.That(entities.System<DecalSystem>().TryAddDecal("Dirt", new EntityCoordinates(second, 1, 1), out _));
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, welder));
            Assert.That(entities.System<ItemToggleSystem>().TryActivate(welder, user));
            if (scenario == "unlit")
                entities.System<ItemToggleSystem>().TryDeactivate(welder, user);

            entities.EventBus.RaiseLocalEvent(welder,
                new BeforeRangedInteractEvent(user, welder, null, new EntityCoordinates(first, (Vector2) firstTile + new Vector2(0.5f)), true));
            entities.EventBus.RaiseLocalEvent(welder,
                new BeforeRangedInteractEvent(user, welder, null, new EntityCoordinates(second, (Vector2) secondTile + new Vector2(0.5f)), true));

            if (succeeds || scenario is "drift-during-welding" or "dropped-welder" or "removed-tile" or "shared-spin")
                Assert.That(entities.GetComponent<GridWeldingComponent>(welder).DoAfter, Is.Not.Null);
            if (scenario == "drift-during-welding")
                transform.SetWorldPosition(second, new Vector2(1.2f, 0));
            if (scenario == "dropped-welder")
                entities.System<SharedHandsSystem>().TryDrop(user, welder);
            if (scenario == "removed-tile")
                maps.SetTile(b, secondTile, Tile.Empty);
        });

        await pair.RunSeconds(6);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(consumed), Is.EqualTo(!succeeds));
            Assert.That(entities.EntityExists(survivor), Is.True);
            if (succeeds)
            {
                Assert.That(entities.GetComponent<TransformComponent>(item).GridUid, Is.EqualTo(survivor));
                var body = entities.GetComponent<PhysicsComponent>(survivor);
                if (scenario is "large-first-rotation-snap" or "large-rotation-snap")
                    Assert.That(body.BodyType, Is.EqualTo(BodyType.Static));
                else
                {
                    Assert.That(body.BodyType, Is.EqualTo(BodyType.Dynamic));
                    Assert.That(body.LinearVelocity.Length(), Is.GreaterThan(0.1f));
                }
                if (scenario is "one-tile-gap" or "slight-angle" or "offset-and-angle")
                {
                    var grid = entities.GetComponent<MapGridComponent>(survivor);
                    var maps = entities.System<SharedMapSystem>();
                    Assert.That(maps.GetTileRef(survivor, grid, Vector2i.Zero).Tile.IsEmpty, Is.False);
                    Assert.That(maps.GetTileRef(survivor, grid, new Vector2i(1, 0)).Tile.IsEmpty, Is.False,
                        "The incoming tile must snap next to the selected edge, closing the gap.");
                }
                if (scenario == "quarter-turn")
                {
                    var decal = entities.System<DecalSystem>().GetDecalsIntersecting(survivor, new Box2(0, 0, 1, 1)).Single().Decal;
                    Assert.That(decal.Coordinates.X, Is.EqualTo(0.2f).Within(0.001));
                    Assert.That(decal.Coordinates.Y, Is.EqualTo(0).Within(0.001));
                }
                if (scenario == "lattice-survivor")
                    Assert.That(entities.System<DecalSystem>().GetDecalsIntersecting(survivor, new Box2(1, 1, 2, 2)).Count,
                        Is.EqualTo(1), "A lattice destination must accept decals on incoming floor tiles.");
            }
        });
        if (scenario == "quarter-turn")
        {
            EntityUid third = default;
            await server.WaitPost(() =>
            {
                var transform = entities.System<SharedTransformSystem>();
                var manager = server.ResolveDependency<IMapManager>();
                var destination = manager.CreateGridEntity(entities.GetComponent<TransformComponent>(first).MapID);
                third = destination.Owner;
                var tile = new Tile(server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
                entities.System<SharedMapSystem>().SetTiles(destination,
                    [(new Vector2i(0, -2), tile), (new Vector2i(1, -2), tile), (new Vector2i(2, -2), tile),
                        (new Vector2i(3, -2), tile), (new Vector2i(4, -2), tile)]);
                transform.SetWorldPositionRotation(third, transform.GetWorldPosition(first), transform.GetWorldRotation(first));
                entities.System<SharedPhysicsSystem>().SetLinearVelocity(third, entities.GetComponent<PhysicsComponent>(first).LinearVelocity);
                entities.EventBus.RaiseLocalEvent(welder,
                    new BeforeRangedInteractEvent(user, welder, null, new EntityCoordinates(first, 0.5f, -0.5f), true));
                entities.EventBus.RaiseLocalEvent(welder,
                    new BeforeRangedInteractEvent(user, welder, null, new EntityCoordinates(third, 0.5f, -1.5f), true));
                Assert.That(entities.GetComponent<GridWeldingComponent>(welder).DoAfter, Is.Not.Null);
            });
            await pair.RunSeconds(6);
            await server.WaitAssertion(() =>
            {
                Assert.That(entities.EntityExists(first), Is.False);
                Assert.That(entities.System<DecalSystem>().GetDecalsIntersecting(third, new Box2(0, 0, 1, 1)).Count,
                    Is.EqualTo(1), "An offset decal must survive welding the combined fragment a second time.");
            });
        }
        await server.WaitPost(() => entities.DeleteEntity(map));
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task WeldsActuallySplitGridBackTogether(bool growFragment)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var entities = server.EntMan;
        var splitWasEnabled = server.CfgMan.GetCVar(CVars.GridSplitting);
        EntityUid first = default;
        EntityUid fragment = default;
        EntityUid map = default;
        EntityUid machine = default;

        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(CVars.GridSplitting, true);
            var maps = entities.System<SharedMapSystem>();
            map = maps.CreateMap(out var mapId);
            var manager = server.ResolveDependency<IMapManager>();
            var grid = manager.CreateGridEntity(mapId);
            first = grid.Owner;
            var tile = new Tile(server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            maps.SetTiles(grid, [(new Vector2i(0, -1), tile), (new Vector2i(0, 0), tile), (new Vector2i(0, 1), tile),
                (new Vector2i(1, 0), tile), (new Vector2i(2, 0), tile), (new Vector2i(2, 1), tile)]);
            machine = entities.SpawnEntity("WallSolid", new EntityCoordinates(first, 2.5f, 1.5f));
            var generator = entities.SpawnEntity(null, new EntityCoordinates(first, 2.5f, 1.5f));
            var transform = entities.System<SharedTransformSystem>();
            transform.AnchorEntity(generator);
            entities.AddComponent<GravityGeneratorComponent>(generator);
            var activated = new ChargedMachineActivatedEvent();
            entities.EventBus.RaiseLocalEvent(generator, ref activated);
            Assert.That(entities.GetComponent<GravityComponent>(first).Enabled, Is.True);
            var physics = entities.System<SharedPhysicsSystem>();
            physics.SetLinearVelocity(first, new Vector2(2, 0));

            maps.SetTile(grid, new Vector2i(1, 0), Tile.Empty);
            fragment = manager.GetAllGrids(mapId).Single(g => g.Owner != first).Owner;
            Assert.That(entities.GetComponent<TransformComponent>(machine).GridUid, Is.EqualTo(fragment));
            Assert.That(entities.GetComponent<PhysicsComponent>(fragment).BodyType, Is.EqualTo(BodyType.Dynamic));
            Assert.That(entities.GetComponent<GravityComponent>(first).Enabled, Is.False);

            if (growFragment)
            {
                var part = entities.GetComponent<Robust.Shared.Map.Components.MapGridComponent>(fragment);
                maps.SetTiles(fragment, part, [(new Vector2i(2, 2), tile), (new Vector2i(2, 3), tile)]);
            }

            transform.SetWorldPosition(fragment, new Vector2(-1, 0));
            var user = entities.SpawnEntity("MobHuman", new EntityCoordinates(first, 0.5f, 0.5f));
            var welder = entities.SpawnEntity("Welder", new EntityCoordinates(first, 0.5f, 0.5f));
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, welder));
            Assert.That(entities.System<ItemToggleSystem>().TryActivate(welder, user));

            var interaction = entities.System<SharedInteractionSystem>();
            interaction.UserInteraction(user, new EntityCoordinates(first, 0.5f, 0.5f), null);
            interaction.UserInteraction(user, new EntityCoordinates(fragment, 2.5f, 0.5f), null);
        });

        await pair.RunSeconds(6);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(fragment), Is.False);
            Assert.That(entities.GetComponent<TransformComponent>(machine).GridUid, Is.EqualTo(first));
            Assert.That(entities.GetComponent<TransformComponent>(machine).Anchored, Is.True);
            Assert.That(entities.GetComponent<PhysicsComponent>(first).BodyType, Is.EqualTo(BodyType.Dynamic));
            Assert.That(entities.GetComponent<GravityComponent>(first).Enabled, Is.True,
                "The restored active generator must enable gravity on the surviving ship.");
        });
        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(CVars.GridSplitting, splitWasEnabled);
            entities.DeleteEntity(map);
        });
        await pair.CleanReturnAsync();
    }
}
