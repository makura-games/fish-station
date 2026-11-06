#nullable enable
using System.Numerics;
using System.Text;
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
    private const int PollTicks = 30;
    private const int StableTicks = 3;

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

    /// <summary>
    /// Текстовый дамп контактов гридов (и двери, если она есть) для сообщения об ошибке.
    /// </summary>
    private static string Describe(
        IEntityManager entMan,
        SharedPhysicsSystem physics,
        EntityUid a,
        EntityUid b,
        EntityUid? door)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("--- diagnostics ---");

        var contacts = physics.GetContacts(a);
        var any = false;
        while (contacts.MoveNext(out Contact? c))
        {
            any = true;
            sb.AppendLine(
                $"contact A={c.EntityA} B={c.EntityB} Enabled={c.Enabled} Deleting={c.Deleting} Touching={c.IsTouching}");
        }

        if (!any)
            sb.AppendLine("no contacts on gridA");

        foreach (var g in new[] { a, b })
        {
            if (entMan.TryGetComponent(g, out PhysicsComponent? body))
            {
                sb.AppendLine(
                    $"grid {g}: BodyType={body.BodyType} CanCollide={body.CanCollide} LinVel={body.LinearVelocity} AngVel={body.AngularVelocity}");
            }
        }

        if (door is { } d && entMan.EntityExists(d))
        {
            if (entMan.TryGetComponent(d, out DoorComponent? doorComp))
                sb.AppendLine($"door state={doorComp.State}");

            if (entMan.TryGetComponent(d, out PhysicsComponent? doorBody))
                sb.AppendLine($"door CanCollide={doorBody.CanCollide}");

            if (entMan.TryGetComponent(d, out FixturesComponent? fixtures))
            {
                foreach (var (id, fix) in fixtures.Fixtures)
                {
                    sb.AppendLine(
                        $"door fixture {id}: Hard={fix.Hard} Layer={fix.CollisionLayer} Mask={fix.CollisionMask}");
                }
            }
        }

        return sb.ToString();
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

        // Возвращает гриды в исходную точку и гасит остаточное движение после расталкивания.
        void Reset()
        {
            foreach (var g in new EntityUid[] { gridA, gridB })
            {
                xformSys.SetWorldPosition(g, Vector2.Zero);
                physSys.SetLinearVelocity(g, Vector2.Zero);
                physSys.SetAngularVelocity(g, 0f);
            }
        }

        // Ждёт, пока GridsTouching станет равным expected (до maxTicks тиков).
        async Task<bool> WaitForTouching(bool expected, int maxTicks = PollTicks)
        {
            for (var i = 0; i < maxTicks; i++)
            {
                var ok = false;
                await server.WaitAssertion(() =>
                {
                    Reset();
                    ok = GridsTouching(physSys, gridA, gridB) == expected;
                });

                if (ok)
                    return true;

                await server.WaitRunTicks(1);
            }

            return false;
        }

        // Состояние должно сохраняться несколько тиков подряд, а не мигнуть на одном.
        async Task<bool> StaysTouching(bool expected, int ticks = StableTicks)
        {
            for (var i = 0; i < ticks; i++)
            {
                await server.WaitRunTicks(1);

                var ok = false;
                await server.WaitAssertion(() =>
                {
                    Reset();
                    ok = GridsTouching(physSys, gridA, gridB) == expected;
                });

                if (!ok)
                    return false;
            }

            return true;
        }

        string Diag()
        {
            var text = "";
            server.WaitPost(() => text = Describe(entMan, physSys, gridA, gridB,
                airlock == default ? null : airlock)).Wait();
            return text;
        }

        await server.WaitAssertion(() =>
        {
            var mapId = testMap.MapId;
            gridA = mapSys.CreateGridEntity(mapId);
            gridB = mapSys.CreateGridEntity(mapId);

            mapSys.SetTile(gridA, gridA, Vector2i.Zero, new Tile(steelId));
            mapSys.SetTile(gridB, gridB, Vector2i.Zero, new Tile(transparentId));

            var physicsA = entMan.GetComponent<PhysicsComponent>(gridA);
            var physicsB = entMan.GetComponent<PhysicsComponent>(gridB);
            physSys.SetBodyType(gridA, BodyType.Dynamic, body: physicsA);
            physSys.SetBodyType(gridB, BodyType.Dynamic, body: physicsB);

            Reset();

            var broadphase = entMan.System<SharedBroadphaseSystem>();
            broadphase.RegenerateContacts(gridA, physicsA);
            broadphase.RegenerateContacts(gridB, physicsB);
        });

        // 1. Прозрачный тайл без препятствий не даёт hard collision.
        Assert.That(await WaitForTouching(false), Is.True,
            "Прозрачный тайл без препятствий не должен давать hard collision" + Diag());
        Assert.That(await StaysTouching(false), Is.True,
            "Контакт появился на прозрачном тайле без препятствий" + Diag());

        // 2. Стена восстанавливает collision.
        await server.WaitAssertion(() =>
        {
            wall = entMan.SpawnEntity("WallSolid", new EntityCoordinates(gridB, 0.5f, 0.5f));
        });

        Assert.That(await WaitForTouching(true), Is.True,
            "Стена на прозрачном тайле должна восстанавливать collision" + Diag());

        // 3. После удаления стены тайл снова проходим.
        await server.WaitAssertion(() => entMan.DeleteEntity(wall));

        Assert.That(await WaitForTouching(false), Is.True,
            "После удаления стены прозрачный тайл снова проходим" + Diag());
        Assert.That(await StaysTouching(false), Is.True,
            "После удаления стены контакт вернулся" + Diag());

        // 4. Закрытый шлюз блокирует.
        await server.WaitAssertion(() =>
        {
            airlock = entMan.SpawnEntity("Airlock", new EntityCoordinates(gridB, 0.5f, 0.5f));
            Assert.That(entMan.GetComponent<TransformComponent>(airlock).Anchored, Is.True);
            Assert.That(entMan.GetComponent<PhysicsComponent>(airlock).CanCollide, Is.True);
            Assert.That(
                entMan.System<TurfSystem>().IsTileBlocked(gridB, Vector2i.Zero, CollisionGroup.FullTileMask),
                Is.True,
                "IsTileBlocked должен видеть закрытый airlock");

            // Принудительно обновляем контакты обоих гридов после появления блокатора.
            var broadphase = entMan.System<SharedBroadphaseSystem>();
            var bodyA = entMan.GetComponent<PhysicsComponent>(gridA);
            var bodyB = entMan.GetComponent<PhysicsComponent>(gridB);
            broadphase.RegenerateContacts(gridA, bodyA);
            broadphase.RegenerateContacts(gridB, bodyB);
        });

        Assert.That(await WaitForTouching(true), Is.True,
            "Закрытый гермозатвор на прозрачном тайле должен блокировать" + Diag());

        // 5. Открытый шлюз снова делает клетку проходимой.
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent(airlock, out DoorComponent? door), Is.True);
            doorSys.StartOpening(airlock, door);
            doorSys.OnPartialOpen(airlock, door);
        });

        Assert.That(await WaitForTouching(false), Is.True,
            "Открытый гермозатвор снова делает клетку проходимой" + Diag());

        await pair.CleanReturnAsync();
    }
}
