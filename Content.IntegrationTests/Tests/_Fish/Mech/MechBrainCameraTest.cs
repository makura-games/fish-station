using System;
using System.Numerics;
using Content.Client.Eye;
using Content.Server.Gravity;
using Content.Server.Mech.Systems;
using Content.Server.Shuttles.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using ServerMoverController = Content.Server.Physics.Controllers.MoverController;
using ClientInputSystem = Robust.Client.GameObjects.InputSystem;

namespace Content.IntegrationTests.Tests._Fish.Mech;

[TestFixture, NonParallelizable]
public sealed class MechBrainCameraTest
{
    [TestCase("PersonalAI", 90d)]
    [TestCase("PersonalAI", 180d)]
    [TestCase("PositronicBrain", 90d)]
    [TestCase("PositronicBrain", 180d)]
    [TestCase("MMI", 90d)]
    [TestCase("MMI", 180d)]
    public async Task CameraAndRelayedMovementStayAlignedOnRotatedGrids(string prototype, double shuttleAngle)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var session = pair.Player!;
        var previousEntity = session.AttachedEntity;
        EntityUid mech = default;
        EntityUid brain = default;
        Entity<MapGridComponent> shuttle = default;
        var inserted = false;
        var hadInputMover = false;
        var movementTicks = (int) Math.Ceiling(server.Timing.TickRate * 0.5);

        await server.WaitPost(() =>
        {
            PrepareFloor(map.Grid);
            var stationStart = new EntityCoordinates(map.Grid, new Vector2(0.5f));
            mech = entMan.SpawnEntity("MechRipleyBattery", stationStart);
            brain = entMan.SpawnEntity(prototype, stationStart);
            hadInputMover = entMan.HasComponent<InputMoverComponent>(brain);
            server.PlayerMan.SetAttachedEntity(session, brain);
            inserted = entMan.System<MechSystem>().TryInsert(mech, brain);
            shuttle = entMan.System<SharedMapSystem>().CreateGridEntity(map.MapId);
            PrepareFloor(shuttle);
            var transform = entMan.System<SharedTransformSystem>();
            transform.SetLocalPosition(shuttle, new Vector2(50f));
            transform.SetLocalRotation(shuttle, Angle.FromDegrees(shuttleAngle));
        });
        await server.WaitAssertion(() =>
        {
            Assert.That(inserted, Is.True);
            Assert.That(entMan.System<VehicleSystem>().GetOperatorOrNull(mech), Is.EqualTo(brain));
            Assert.That(entMan.GetComponent<VehicleOperatorComponent>(brain).Vehicle, Is.EqualTo(mech));
            Assert.That(entMan.HasComponent<ShuttleComponent>(shuttle), Is.True);
        });
        await pair.RunTicksSync(20);
        try
        {
            await CheckMovement(map.Grid, "station");

            await server.WaitPost(() => entMan.System<SharedTransformSystem>().SetCoordinates(mech,
                new EntityCoordinates(shuttle, new Vector2(0.5f))));
            await pair.RunTicksSync(20);
            await CheckMovement(shuttle, "rotated shuttle");

            foreach (var rotation in new[] { 90d, -90d })
            {
                Angle previousCamera = default;
                await client.WaitAssertion(() => previousCamera = client.ResolveDependency<IEyeManager>().CurrentEye.Rotation);
                await server.WaitPost(() =>
                    entMan.System<ServerMoverController>().RotateCamera(brain, Angle.FromDegrees(rotation)));
                await pair.RunTicksSync(90);
                await client.WaitPost(() => client.System<EyeLerpingSystem>().FrameUpdate(0f));
                await client.WaitAssertion(() => Assert.That(Angle.ShortestDistance(previousCamera,
                    client.ResolveDependency<IEyeManager>().CurrentEye.Rotation).Theta,
                    Is.EqualTo(-Angle.FromDegrees(rotation).Theta).Within(0.02),
                    "The camera must visibly rotate when the player uses a rotation command."));
                await CheckMovement(shuttle, $"shuttle after camera rotation {rotation}");
            }

            var ejected = false;
            await server.WaitPost(() => ejected = entMan.System<MechSystem>().TryEject(mech));
            // Удаление оператора и relay-компонентов завершается в конце тика.
            await pair.RunTicksSync(5);
            await server.WaitAssertion(() =>
            {
                Assert.That(ejected, Is.True);
                Assert.That(entMan.System<VehicleSystem>().GetOperatorOrNull(mech), Is.Null);
                Assert.That(entMan.HasComponent<VehicleOperatorComponent>(brain), Is.False);
                Assert.That(entMan.HasComponent<RelayInputMoverComponent>(brain), Is.False);
                Assert.That(entMan.HasComponent<MovementRelayTargetComponent>(mech), Is.False);
                Assert.That(entMan.HasComponent<InputMoverComponent>(brain), Is.EqualTo(hadInputMover),
                    "Leaving the mech must restore the brain's original movement controller state.");
                Assert.That(entMan.System<ActionBlockerSystem>().CanMove(brain), Is.False);
            });
        }
        finally
        {
            await SetKey(EngineKeyFunctions.MoveUp, BoundKeyState.Up);
            await SetKey(EngineKeyFunctions.MoveRight, BoundKeyState.Up);
            await server.WaitPost(() =>
            {
                server.PlayerMan.SetAttachedEntity(session, previousEntity);
                entMan.DeleteEntity(map.MapUid);
            });
        }
        await pair.CleanReturnAsync();

        void PrepareFloor(Entity<MapGridComponent> grid)
        {
            var mapSystem = entMan.System<SharedMapSystem>();
            for (var x = -10; x <= 10; x++)
            for (var y = -10; y <= 10; y++)
                mapSystem.SetTile(grid, grid.Comp, new Vector2i(x, y), map.Tile.Tile);

            entMan.System<GravitySystem>().EnableGravity(grid);
            entMan.GetComponent<GravityComponent>(grid).Inherent = true;
        }

        async Task SetKey(BoundKeyFunction key, BoundKeyState state)
        {
            await client.WaitPost(() =>
            {
                var clientMech = pair.ToClientUid(mech);
                var input = client.ResolveDependency<IInputManager>();
                var message = new ClientFullInputCmdMessage(client.Timing.CurTick, client.Timing.TickFraction,
                    input.NetworkBindMap.KeyFunctionID(key))
                {
                    State = state,
                    Coordinates = client.EntMan.GetComponent<TransformComponent>(clientMech).Coordinates,
                    Uid = clientMech,
                };
                client.System<ClientInputSystem>().HandleInputCommand(client.Session, key, message);
            });
        }

        async Task CheckMovement(EntityUid expectedGrid, string stage)
        {
            foreach (var (key, cameraDirection) in new[]
                     {
                         (EngineKeyFunctions.MoveUp, Vector2.UnitY),
                         (EngineKeyFunctions.MoveRight, Vector2.UnitX),
                     })
            {
                Matrix3x2 viewMatrix = default;
                Vector2 start = default;
                await client.WaitPost(() => client.System<EyeLerpingSystem>().FrameUpdate(0f));
                await client.WaitAssertion(() =>
                    client.ResolveDependency<IEyeManager>().CurrentEye.GetViewMatrix(out viewMatrix, Vector2.One));
                await server.WaitAssertion(() =>
                {
                    Assert.That(entMan.GetComponent<TransformComponent>(mech).GridUid, Is.EqualTo(expectedGrid));
                    start = entMan.System<SharedTransformSystem>().GetWorldPosition(mech);
                });

                await SetKey(key, BoundKeyState.Down);
                await pair.RunTicksSync(movementTicks);
                await SetKey(key, BoundKeyState.Up);
                // Ждём доставку отпускания клавиши и торможение перед следующим направлением.
                var stopped = false;
                for (var attempt = 0; attempt < 6; attempt++)
                {
                    await pair.RunTicksSync(movementTicks);
                    await server.WaitAssertion(() =>
                        stopped = entMan.GetComponent<PhysicsComponent>(mech).LinearVelocity.LengthSquared() < 0.0001f);
                    if (stopped)
                        break;
                }

                await server.WaitAssertion(() =>
                {
                    Assert.That(stopped, Is.True, "The mech must stop after the movement key is released.");
                    var displacement = entMan.System<SharedTransformSystem>().GetWorldPosition(mech) - start;
                    Assert.That(displacement.Length(), Is.GreaterThan(0.1f),
                        $"{prototype} must move the mech through client input on {stage} ({key}).");

                    // Проецируем серверное смещение настоящей камерой, независимо от расчёта mover.
                    var cameraDisplacement = Vector2.TransformNormal(displacement, viewMatrix);
                    Assert.That(Vector2.Dot(Vector2.Normalize(cameraDisplacement), cameraDirection), Is.GreaterThan(0.98f),
                        $"{prototype} must move in the pressed screen direction on {stage} ({key}).");
                    Assert.That(entMan.GetComponent<TransformComponent>(mech).GridUid, Is.EqualTo(expectedGrid),
                        "Movement must remain on the prepared floor rather than leave it and drift in space.");
                });
            }
        }
    }
}
