#nullable enable
using System.Numerics;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics.Contacts;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests._Fish;

/// <summary>
/// Verifies that tiles with enableGridCollision: false do not hard-collide with other grids
/// unless a dense anchored blocker (wall / closed airlock) occupies the cell.
/// </summary>
[TestFixture]
public sealed class TransparentTileGridCollisionTest
{
    private static bool GridsTouching(SharedPhysicsSystem physics, EntityUid a, EntityUid b)
    {
        var contacts = physics.GetContacts(a);
        while (contacts.MoveNext(out Contact? contact))
        {
            if (contact.Deleting || !contact.Enabled)
                continue;

            if (contact.EntityA != b && contact.EntityB != b)
                continue;

            // Для grid contacts IsTouching может ещё не обновиться — наличие enabled contact достаточно.
            return true;
        }

        return false;
    }

    [Test]
    public async Task TransparentTilePassesUntilBlocked()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.EntMan;
        var mapSys = entMan.System<SharedMapSystem>();
        var physSys = entMan.System<SharedPhysicsSystem>();
        var doorSys = entMan.System<SharedDoorSystem>();
        var tileMan = server.ResolveDependency<ITileDefinitionManager>();
        var xformSys = entMan.System<SharedTransformSystem>();

        var steelId = tileMan["FloorSteel"].TileId;
        var transparentId = tileMan["FloorTransparent"].TileId;

        var testMap = await pair.CreateTestMap();

        Entity<MapGridComponent> gridA = default;
        Entity<MapGridComponent> gridB = default;
        EntityUid wall = default;
        EntityUid airlock = default;

        await server.WaitAssertion(() =>
        {
            var mapId = testMap.MapId;
            gridA = mapSys.CreateGridEntity(mapId);
            gridB = mapSys.CreateGridEntity(mapId);

            mapSys.SetTile(gridA, gridA, Vector2i.Zero, new Tile(steelId));
            mapSys.SetTile(gridB, gridB, Vector2i.Zero, new Tile(transparentId));

            xformSys.SetWorldPosition(gridA, Vector2.Zero);
            xformSys.SetWorldPosition(gridB, Vector2.Zero);

            var physicsA = entMan.GetComponent<PhysicsComponent>(gridA);
            var physicsB = entMan.GetComponent<PhysicsComponent>(gridB);
            physSys.SetBodyType(gridA, BodyType.Dynamic, body: physicsA);
            physSys.SetBodyType(gridB, BodyType.Dynamic, body: physicsB);

            var broadphase = entMan.System<SharedBroadphaseSystem>();
            broadphase.RegenerateContacts(gridA, physicsA);
            broadphase.RegenerateContacts(gridB, physicsB);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            xformSys.SetWorldPosition(gridA, Vector2.Zero);
            xformSys.SetWorldPosition(gridB, Vector2.Zero);
            Assert.That(GridsTouching(physSys, gridA, gridB), Is.False,
                "Прозрачный тайл без препятствий не должен давать hard collision");
        });

        await server.WaitAssertion(() =>
        {
            wall = entMan.SpawnEntity("WallSolid", new EntityCoordinates(gridB, 0.5f, 0.5f));
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            xformSys.SetWorldPosition(gridA, Vector2.Zero);
            xformSys.SetWorldPosition(gridB, Vector2.Zero);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(GridsTouching(physSys, gridA, gridB), Is.True,
                "Стена на прозрачном тайле должна восстанавливать collision");
            entMan.DeleteEntity(wall);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            xformSys.SetWorldPosition(gridA, Vector2.Zero);
            xformSys.SetWorldPosition(gridB, Vector2.Zero);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(GridsTouching(physSys, gridA, gridB), Is.False,
                "После удаления стены прозрачный тайл снова проходим");

            airlock = entMan.SpawnEntity("Airlock", new EntityCoordinates(gridB, 0.5f, 0.5f));
            Assert.That(entMan.GetComponent<TransformComponent>(airlock).Anchored, Is.True);
            Assert.That(entMan.GetComponent<PhysicsComponent>(airlock).CanCollide, Is.True);
            Assert.That(entMan.System<TurfSystem>().IsTileBlocked(gridB, Vector2i.Zero, CollisionGroup.FullTileMask), Is.True,
                "IsTileBlocked должен видеть закрытый airlock");

            // Принудительно обновляем контакты обоих гридов после появления блокатора.
            var broadphase = entMan.System<SharedBroadphaseSystem>();
            var bodyA = entMan.GetComponent<PhysicsComponent>(gridA);
            var bodyB = entMan.GetComponent<PhysicsComponent>(gridB);
            broadphase.RegenerateContacts(gridA, bodyA);
            broadphase.RegenerateContacts(gridB, bodyB);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            xformSys.SetWorldPosition(gridA, Vector2.Zero);
            xformSys.SetWorldPosition(gridB, Vector2.Zero);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(GridsTouching(physSys, gridA, gridB), Is.True,
                "Закрытый гермозатвор на прозрачном тайле должен блокировать");

            Assert.That(entMan.TryGetComponent(airlock, out DoorComponent? door), Is.True);
            doorSys.StartOpening(airlock, door);
            doorSys.OnPartialOpen(airlock, door);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            xformSys.SetWorldPosition(gridA, Vector2.Zero);
            xformSys.SetWorldPosition(gridB, Vector2.Zero);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(GridsTouching(physSys, gridA, gridB), Is.False,
                "Открытый гермозатвор снова делает клетку проходимой");
        });

        await pair.CleanReturnAsync();
    }
}
