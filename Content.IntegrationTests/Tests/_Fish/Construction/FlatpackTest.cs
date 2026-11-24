using Content.Shared.Gravity;
using Content.Shared.Interaction;
using NUnit.Framework;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests._Fish.Construction;

[TestFixture]
public sealed class FlatpackTest
{
    [Test]
    public async Task GravityGeneratorFlatpackRequiresFree3x3()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapSystem = server.System<MapSystem>();
        var transform = server.System<SharedTransformSystem>();
        var bus = entMan.EventBus;

        MapId mapId = default;
        EntityUid grid = default;
        EntityUid multitool = default;
        EntityUid user = default;

        await server.WaitPost(() =>
        {
            mapSystem.CreateMap(out mapId);
            var gridEnt = mapSystem.CreateGridEntity(mapId);
            grid = gridEnt.Owner;

            for (var x = -1; x <= 6; x++)
            {
                for (var y = -1; y <= 1; y++)
                    mapSystem.SetTile(gridEnt, new Vector2i(x, y), new Tile(1));
            }

            multitool = entMan.SpawnEntity("Multitool", MapCoordinates.Nullspace);
            user = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
        });

        EntityUid freeFlatpack = default;
        await server.WaitPost(() =>
        {
            freeFlatpack = entMan.SpawnEntity("FishGravityGeneratorFlatpack", new EntityCoordinates(grid, 0, 0));
            var xform = entMan.GetComponent<TransformComponent>(freeFlatpack);
            bus.RaiseLocalEvent(freeFlatpack, new InteractUsingEvent(user, multitool, freeFlatpack, xform.Coordinates));
        });

        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(freeFlatpack), Is.False,
                "Flatpack must be consumed when the whole 3x3 area is free");
            Assert.That(entMan.Count<GravityGeneratorComponent>(), Is.EqualTo(1),
                "Flatpack must create a gravity generator when the whole 3x3 area is free");
        });

        EntityUid blockedFlatpack = default;
        await server.WaitPost(() =>
        {
            var wall = entMan.SpawnEntity("WallSolid", new EntityCoordinates(grid, 6, 0));
            var wallXform = entMan.GetComponent<TransformComponent>(wall);
            if (!wallXform.Anchored)
                transform.AnchorEntity(wall, wallXform);

            blockedFlatpack = entMan.SpawnEntity("FishGravityGeneratorFlatpack", new EntityCoordinates(grid, 5, 0));
            var xform = entMan.GetComponent<TransformComponent>(blockedFlatpack);
            bus.RaiseLocalEvent(blockedFlatpack, new InteractUsingEvent(user, multitool, blockedFlatpack, xform.Coordinates));
        });

        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(blockedFlatpack), Is.True,
                "Flatpack must not be deleted when a tile in the 3x3 area is blocked");
            Assert.That(entMan.Count<GravityGeneratorComponent>(), Is.EqualTo(1),
                "Flatpack must not create a gravity generator when a tile in the 3x3 area is blocked");
        });

        await server.WaitPost(() => mapSystem.DeleteMap(mapId));
        await pair.CleanReturnAsync();
    }
}
