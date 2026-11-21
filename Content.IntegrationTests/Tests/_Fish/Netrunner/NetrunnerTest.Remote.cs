using System.Numerics;
using Content.Client._Fish.Netrunner;
using Content.Server._Fish.Netrunner;
using Content.Server.Body.Components;
using Content.Server.Power.Components;
using Content.Server.SurveillanceCamera;
using Content.Shared._Fish.Netrunner;
using Content.Shared.Buckle;
using Content.Shared.Implants;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Station.Components;
using Content.Shared.StationAi;
using Content.Shared.Tag;
using NUnit.Framework;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;

namespace Content.IntegrationTests.Tests._Fish.Netrunner;

public sealed partial class NetrunnerTest
{
    [TestCase(90)]
    [TestCase(180)]
    public async Task ChairCameraAndRelayedMovementShareTheSameOrientation(int rotation)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid remote = default;
        NetEntity userNet = default;
        NetEntity remoteNet = default;
        Vector2 before = default;
        Vector2 bodyBefore = default;
        Angle eyeRotation = default;
        bool connected = false;
        await Server.WaitPost(() =>
        {
            var transform = SEntMan.System<SharedTransformSystem>();
            transform.SetLocalRotation(map.Grid, Angle.FromDegrees(rotation));
            var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.EnsureComponent<StationMemberComponent>(map.Grid).Station = station;
            for (var i = 1; i <= 10; i++)
                SEntMan.System<SharedMapSystem>().SetTile(map.Grid, map.Grid.Comp,
                    new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + Vector2.UnitX * i), map.Tile.Tile);
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            userNet = SEntMan.GetNetEntity(user);
            SEntMan.RemoveComponent<RespiratorComponent>(user);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            var implant = SEntMan.System<SharedSubdermalImplantSystem>().AddImplant(user, "FishNetrunnerImplantDebug")!.Value;
            var chair = SEntMan.SpawnEntity("FishNetrunnerChair", map.GridCoords);
            SetPowered(chair);
            var camera = SEntMan.SpawnEntity("SurveillanceCameraEngineering",
                new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + Vector2.UnitX));
            SetPowered(camera);
            connected = SEntMan.System<SharedBuckleSystem>().TryBuckle(user, user, chair);
            remote = SEntMan.GetComponent<NetrunnerImplantComponent>(implant).RemoteEye!.Value;
            remoteNet = SEntMan.GetNetEntity(remote);
            before = transform.GetWorldPosition(remote);
            bodyBefore = transform.GetWorldPosition(user);
        });
        await Pair.RunUntilSynced();
        await Client.WaitPost(() =>
        {
            Client.System<NetrunnerRemoteViewSystem>().FrameUpdate(0);
            eyeRotation = CEntMan.GetComponent<EyeComponent>(CEntMan.GetEntity(remoteNet)).Eye!.Rotation;
        });
        await Client.WaitAssertion(() =>
        {
            var eye = CEntMan.GetComponent<EyeComponent>(CEntMan.GetEntity(remoteNet));
            Assert.That(Client.ResolveDependency<IEyeManager>().CurrentEye, Is.SameAs(eye.Eye));
            Assert.That(eyeRotation.EqualsApprox(-Angle.FromDegrees(rotation), 0.001), Is.True);
        });
        // Fish-edit: Проверяем нажатие клавиши с клиента и передачу ввода камере через relay.
        await SetRemoteMovementKey(remoteNet, BoundKeyState.Down);
        await Pair.RunSeconds(0.75f);
        await SetRemoteMovementKey(remoteNet, BoundKeyState.Up);
        await Pair.RunUntilSynced();
        await Server.WaitAssertion(() =>
        {
            Assert.That(connected, Is.True);
            var transforms = SEntMan.System<SharedTransformSystem>();
            var onScreen = eyeRotation.RotateVec(transforms.GetWorldPosition(remote) - before);
            Assert.That(onScreen.X, Is.GreaterThan(3), "Right input must move the camera right on screen.");
            Assert.That(MathF.Abs(onScreen.Y), Is.LessThan(0.1f));
            Assert.That(Vector2.Distance(transforms.GetWorldPosition(user), bodyBefore), Is.LessThan(0.001f),
                "Relayed movement must leave the body in the chair.");
        });
        await Server.WaitPost(() => SEntMan.System<SharedBuckleSystem>().TryUnbuckle(user, user));
        await Pair.RunUntilSynced();
        await Client.WaitPost(() => Client.System<NetrunnerRemoteViewSystem>().FrameUpdate(0));
        await Client.WaitAssertion(() => Assert.That(Client.ResolveDependency<IEyeManager>().CurrentEye,
            Is.SameAs(CEntMan.GetComponent<EyeComponent>(CEntMan.GetEntity(userNet)).Eye)));
    }

    private async Task SetRemoteMovementKey(NetEntity remote, BoundKeyState state)
    {
        await Client.WaitPost(() =>
        {
            var viewer = CEntMan.GetEntity(remote);
            var key = EngineKeyFunctions.MoveRight;
            var functionId = Client.ResolveDependency<IInputManager>().NetworkBindMap.KeyFunctionID(key);
            var message = new ClientFullInputCmdMessage(CGameTiming.CurTick, CGameTiming.TickFraction, functionId)
            {
                State = state,
                Coordinates = CEntMan.GetComponent<TransformComponent>(viewer).Coordinates,
                Uid = viewer,
            };
            Client.System<Robust.Client.GameObjects.InputSystem>().HandleInputCommand(Client.Session, key, message);
        });
    }

    [Test]
    public async Task RemoteViewerMovesThroughWallsAndPowerLossRestoresBody()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid chair = default;
        EntityUid implant = default;
        EntityUid remote = default;
        Vector2 bodyPosition = default;
        Vector2 viewerPosition = default;
        await Server.WaitPost(() =>
        {
            var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.EnsureComponent<StationMemberComponent>(map.Grid).Station = station;
            for (var i = 1; i <= 9; i++)
                SEntMan.System<SharedMapSystem>().SetTile(map.Grid, map.Grid.Comp,
                    new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + Vector2.UnitX * i), map.Tile.Tile);
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            SEntMan.RemoveComponent<RespiratorComponent>(user);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            implant = SEntMan.System<SharedSubdermalImplantSystem>().AddImplant(user, "FishNetrunnerImplant")!.Value;
            SEntMan.System<TagSystem>().AddTag(user, InstantDoAftersTag);
            chair = SEntMan.SpawnEntity("FishNetrunnerChair", map.GridCoords);
            SetPowered(chair);
            var camera = SEntMan.SpawnEntity("SurveillanceCameraEngineering",
                new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + Vector2.UnitX));
            SetPowered(camera);
            SEntMan.SpawnEntity("WallSolid", new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + Vector2.UnitX * 3));
            SEntMan.System<SharedBuckleSystem>().TryBuckle(user, user, chair);
            remote = SEntMan.GetComponent<NetrunnerImplantComponent>(implant).RemoteEye!.Value;
            bodyPosition = SEntMan.System<SharedTransformSystem>().GetWorldPosition(user);
            viewerPosition = SEntMan.System<SharedTransformSystem>().GetWorldPosition(remote);
            SEntMan.System<SharedMoverController>().SetVelocityDirection(
                (remote, SEntMan.GetComponent<InputMoverComponent>(remote)), Direction.East, 0, true);
        });
        await Pair.RunSeconds(0.75f);
        await Server.WaitAssertion(() =>
        {
            var transforms = SEntMan.System<SharedTransformSystem>();
            Assert.That(transforms.GetWorldPosition(remote).X, Is.GreaterThan(viewerPosition.X + 3));
            Assert.That(transforms.GetWorldPosition(user), Is.EqualTo(bodyPosition));
            Assert.That(ServerSession!.AttachedEntity, Is.EqualTo(user));
        });
        await Server.WaitPost(() =>
        {
            var receiver = SEntMan.GetComponent<ApcPowerReceiverComponent>(chair);
            SEntMan.System<SharedPowerReceiverSystem>().SetNeedsPower(chair, true, receiver);
            receiver.Powered = false;
            var power = new PowerChangedEvent(false, 0);
            SEntMan.EventBus.RaiseLocalEvent(chair, ref power);
        });
        await Pair.RunUntilSynced();
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<NetrunnerImplantComponent>(implant).RemoteEye, Is.Null);
            Assert.That(SEntMan.GetComponent<EyeComponent>(user).Target, Is.Null);
            Assert.That(SEntMan.GetComponent<EyeComponent>(user).DrawFov, Is.True);
            Assert.That(SEntMan.HasComponent<RelayInputMoverComponent>(user), Is.False);
            Assert.That(SEntMan.EntityExists(remote), Is.False);
        });
    }

    [Test]
    public async Task OnlyActiveCamerasFromAuthorizedStationSupplyVision()
    {
        var map = await Pair.CreateTestMap();
        EntityUid camera = default;
        EntityUid station = default;
        EntityUid otherStation = default;
        Vector2i tile = default;
        await Server.WaitPost(() =>
        {
            station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            otherStation = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.EnsureComponent<StationMemberComponent>(map.Grid).Station = station;
            camera = SEntMan.SpawnEntity("SurveillanceCameraEngineering", map.GridCoords);
            SetPowered(camera);
            var fakeSource = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<StationAiVisionComponent>(fakeSource);
            tile = SEntMan.System<SharedMapSystem>().LocalToTile(map.Grid, map.Grid.Comp, map.GridCoords);
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            var vision = SEntMan.System<StationAiVisionSystem>();
            var broadphase = SEntMan.GetComponent<BroadphaseComponent>(map.Grid);
            Assert.That(vision.IsNetrunnerAccessible((map.Grid, broadphase, map.Grid.Comp), tile, station), Is.True);
            Assert.That(vision.IsNetrunnerAccessible((map.Grid, broadphase, map.Grid.Comp), tile, otherStation), Is.False);
        });
        await Server.WaitPost(() => SEntMan.System<SurveillanceCameraSystem>().SetActive(camera, false));
        await Server.WaitAssertion(() =>
        {
            var broadphase = SEntMan.GetComponent<BroadphaseComponent>(map.Grid);
            Assert.That(SEntMan.System<StationAiVisionSystem>().IsNetrunnerAccessible(
                (map.Grid, broadphase, map.Grid.Comp), tile, station), Is.False);
        });
    }
}
