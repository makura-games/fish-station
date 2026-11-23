using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Fish.Netrunner;
using Content.Server.Power.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared._Fish.Netrunner;
using Content.Server.Body.Components;
using Content.Shared.Buckle;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Inventory;
using Content.Shared.Light.Components;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Station.Components;
using Content.Shared.Stunnable;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared.Tag;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Temperature.Components;
using NUnit.Framework;
using Robust.Client.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.Netrunner;

/// <summary>Lifecycle and mindshield targeting contracts for the netrunner implant.</summary>
[TestFixture]
public sealed partial class NetrunnerTest : GameTest
{
    private static readonly ProtoId<TagPrototype> InstantDoAftersTag = "InstantDoAfters";
    private static readonly ProtoId<NetrunnerAbilityPrototype> NeuralOverload = "FishNetrunnerNeuralOverload";

    [TestCase("FishNetrunnerOpenAirlock", false)]
    [TestCase("FishNetrunnerDisableCamera", false)]
    [TestCase("FishNetrunnerDisableLight", false)]
    [TestCase("FishNetrunnerDisableSensors", false)]
    [TestCase("FishNetrunnerDisableHeadset", false)]
    [TestCase("FishNetrunnerNeuralOverload", false)]
    [TestCase("FishNetrunnerNeuralOverload", true)]
    [TestCase("FishNetrunnerShortCircuit", false)]
    [TestCase("FishNetrunnerMotorInterference", false)]
    [TestCase("FishNetrunnerOpticalGlitch", false)]
    [TestCase("FishNetrunnerLockWeapon", false)]
    [TestCase("FishNetrunnerGripShock", false)]
    [TestCase("FishNetrunnerNeuralStatic", false)]
    [TestCase("FishNetrunnerThermalSurge", false)]
    [TestCase("FishNetrunnerBatteryDrain", false)]
    [TestCase("FishNetrunnerLockAirlock", false)]
    [TestCase("FishNetrunnerEraseTrace", false)]
    public async Task ProgramStartsFromClientAction(string abilityId, bool instant)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid target = default;
        EntityUid device = default;
        EntityUid implant = default;
        NetEntity actionNet = default;
        NetEntity targetNet = default;
        NetCoordinates areaNet = default;
        EntityUid extraLight = default;
        EntityUid outsideLight = default;
        float originalTemperature = 0;
        float originalCharge = 0;
        var implants = SEntMan.System<SharedSubdermalImplantSystem>();
        var ability = SProtoMan.Index<NetrunnerAbilityPrototype>(abilityId);

        await Server.WaitPost(() =>
        {
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var targetPrototype = ability.Kind switch
            {
                NetrunnerHackKind.OpenAirlock or NetrunnerHackKind.LockAirlock or NetrunnerHackKind.EraseTrace => "Airlock",
                NetrunnerHackKind.DisableCamera => "SurveillanceCameraEngineering",
                NetrunnerHackKind.DisableLight => "PoweredSmallLight",
                NetrunnerHackKind.ShortCircuit => "PlayerBorgBattery",
                NetrunnerHackKind.LockWeapon or NetrunnerHackKind.BatteryDrain => "WeaponLaserCarbine",
                _ => "MobHuman",
            };
            var targetCoordinates = new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + Vector2.UnitX);
            SEntMan.System<SharedMapSystem>().SetTile(map.Grid, map.Grid.Comp, targetCoordinates, map.Tile.Tile);
            target = SEntMan.SpawnEntity(targetPrototype, targetCoordinates);
            device = target;
            SEntMan.RemoveComponent<RespiratorComponent>(user);
            SEntMan.RemoveComponent<RespiratorComponent>(target);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            implant = implants.AddImplant(user, "FishNetrunnerImplant")!.Value;
            SEntMan.System<NetrunnerSystem>().TryBuyProgram(
                (implant, SEntMan.GetComponent<NetrunnerImplantComponent>(implant)), user, abilityId);
            if (instant)
                SEntMan.System<TagSystem>().AddTag(user, InstantDoAftersTag);
            if (ability.Kind is NetrunnerHackKind.NeuralOverload or NetrunnerHackKind.MotorInterference
                or NetrunnerHackKind.GripShock or NetrunnerHackKind.NeuralStatic or NetrunnerHackKind.ThermalSurge)
                implants.AddImplant(target, "MindShieldImplant");
            if (ability.Kind is NetrunnerHackKind.DisableHeadset or NetrunnerHackKind.DisableSensors)
            {
                var headset = ability.Kind == NetrunnerHackKind.DisableHeadset;
                device = SEntMan.SpawnEntity(headset ? "ClothingHeadsetGrey" : "ClothingUniformJumpsuitColorGrey",
                    map.GridCoords);
                SEntMan.System<InventorySystem>().TryEquip(target, device, headset ? "ears" : "jumpsuit");
                if (!headset)
                    SEntMan.System<SharedSuitSensorSystem>().SetSensor((device, null), SuitSensorMode.SensorCords);
            }
            else if (ability.Kind == NetrunnerHackKind.OpticalGlitch)
            {
                device = SEntMan.SpawnEntity("ClothingEyesHudMedical", map.GridCoords);
                SEntMan.System<InventorySystem>().TryEquip(target, device, "eyes");
            }
            else if (ability.Kind == NetrunnerHackKind.GripShock)
            {
                var item = SEntMan.SpawnEntity("Crowbar", map.GridCoords);
                SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(target, item);
            }
            else if (ability.Kind == NetrunnerHackKind.ThermalSurge)
                originalTemperature = SEntMan.GetComponent<TemperatureComponent>(target).Temperature;
            else if (ability.Kind == NetrunnerHackKind.BatteryDrain)
            {
                SEntMan.System<PowerCellSystem>().TryGetBatteryFromSlotOrEntity((target, null), out var battery);
                device = battery!.Value.Owner;
                originalCharge = SEntMan.System<SharedBatterySystem>().GetCharge(device).Charge;
            }
            else if (ability.Kind == NetrunnerHackKind.EraseTrace)
            {
                SEntMan.AddComponent<NetrunnerAuditComponent>(target).Entries.Add(
                    new NetrunnerLogEntry(SGameTiming.CurTime, NetrunnerHackKind.OpenAirlock, "Airlock", true));
            }
            else if (ability.Kind == NetrunnerHackKind.DisableLight)
            {
                // Fish-edit: Светильники одной области стоят на связном участке пола одной сетки.
                var maps = SEntMan.System<SharedMapSystem>();
                for (var x = 0; x <= 12; x++)
                for (var y = 0; y <= 2; y++)
                    maps.SetTile(map.Grid, map.Grid.Comp,
                        new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + new Vector2(x, y)), map.Tile.Tile);
                var extraCoordinates = new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + new Vector2(3, 1));
                var outsideCoordinates = new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + new Vector2(11, 1));
                extraLight = SEntMan.SpawnEntity("PoweredSmallLight", extraCoordinates);
                outsideLight = SEntMan.SpawnEntity("PoweredSmallLight", outsideCoordinates);
                SetPowered(extraLight);
                SetPowered(outsideLight);
                areaNet = SEntMan.GetNetCoordinates(targetCoordinates);
            }
            SetPowered(device);
            foreach (var action in SEntMan.GetComponent<NetrunnerImplantComponent>(implant).GrantedActions)
            {
                if (SEntMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID == ability.Action)
                    actionNet = SEntMan.GetNetEntity(action);
            }
            targetNet = SEntMan.GetNetEntity(target);
        });
        await Pair.RunUntilSynced();
        await Server.WaitAssertion(() => Assert.That(actionNet, Is.Not.EqualTo(default(NetEntity))));

        await Client.WaitPost(() => CEntMan.RaisePredictiveEvent(ability.AreaRadius > 0
            ? new RequestPerformActionEvent(actionNet, areaNet)
            : new RequestPerformActionEvent(actionNet, targetNet)));
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            var resources = SEntMan.GetComponent<NetrunnerImplantComponent>(implant);
            if (instant)
                Assert.That(resources.Pending, Is.Null);
            else
            {
                Assert.That(resources.Pending, Is.Not.Null, "The player action must start a cast.");
                if (ability.AreaRadius > 0)
                    Assert.That(resources.Pending!.AreaTargets, Has.Count.EqualTo(2));
                else
                    Assert.That(resources.Pending!.Target, Is.EqualTo(target));
                if (ability.Kind == NetrunnerHackKind.NeuralOverload)
                {
                    foreach (var cast in SEntMan.GetComponent<DoAfterComponent>(user).DoAfters.Values)
                        Assert.That(cast.Args.Hidden, Is.True);
                }
            }
            Assert.That(SEntMan.GetComponent<ActionComponent>(SEntMan.GetEntity(actionNet)).Cooldown?.End,
                Is.EqualTo(resources.Cooldowns[abilityId]), "The action button must show the program cooldown.");
        });

        if (!instant)
            await Pair.RunSeconds((float)ability.CastTime.TotalSeconds + 0.5f);
        await Server.WaitAssertion(() =>
        {
            var resources = SEntMan.GetComponent<NetrunnerImplantComponent>(implant);
            Assert.That(resources.Pending, Is.Null);
            Assert.That(resources.Journal, Has.Count.EqualTo(1));
            Assert.That(resources.Journal[0].Success, Is.True);
            Assert.That(resources.Ram, Is.EqualTo(resources.MaxRam - ability.RamCost).Within(0.01f));
            switch (ability.Kind)
            {
                case NetrunnerHackKind.OpenAirlock:
                    Assert.That(SEntMan.GetComponent<DoorComponent>(target).State, Is.EqualTo(DoorState.Open));
                    break;
                case NetrunnerHackKind.NeuralOverload:
                    Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.True);
                    Assert.That(SEntMan.HasComponent<NetrunnerAuditComponent>(target), Is.False);
                    SEntMan.System<SharedAppearanceSystem>().TryGetData<bool>(target,
                        SharedStunSystem.StunVisuals.SeeingStars, out var stars);
                    Assert.That(stars, Is.False);
                    break;
                case NetrunnerHackKind.GripShock:
                    Assert.That(SEntMan.System<SharedHandsSystem>().TryGetActiveItem((target, null), out _), Is.False);
                    break;
                case NetrunnerHackKind.NeuralStatic:
                    Assert.That(SEntMan.GetComponent<StaminaComponent>(target).StaminaDamage, Is.GreaterThan(0).And.LessThanOrEqualTo(30));
                    break;
                case NetrunnerHackKind.ThermalSurge:
                    Assert.That(SEntMan.GetComponent<TemperatureComponent>(target).Temperature, Is.GreaterThan(originalTemperature + 1));
                    break;
                case NetrunnerHackKind.BatteryDrain:
                    var charge = SEntMan.System<SharedBatterySystem>().GetCharge(device);
                    Assert.That(charge.Charge, Is.EqualTo(originalCharge - charge.MaxCharge * 0.1f).Within(0.01f));
                    break;
                case NetrunnerHackKind.EraseTrace:
                    Assert.That(SEntMan.HasComponent<NetrunnerAuditComponent>(target), Is.False);
                    break;
                case NetrunnerHackKind.ShortCircuit:
                    Assert.That(SEntMan.System<DamageableSystem>().GetTotalDamage(target),
                        Is.GreaterThan(FixedPoint2.Zero));
                    Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.True);
                    Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.True);
                    break;
                default:
                    Assert.That(SEntMan.GetComponent<NetrunnerDisruptionComponent>(device).Kind, Is.EqualTo(ability.Kind));
                    if (ability.Kind == NetrunnerHackKind.DisableCamera)
                        Assert.That(SEntMan.GetComponent<SurveillanceCameraComponent>(device).Active, Is.False);
                    if (ability.Kind == NetrunnerHackKind.DisableLight)
                    {
                        Assert.That(SEntMan.GetComponent<PoweredLightComponent>(device).CurrentLit, Is.False);
                        Assert.That(SEntMan.GetComponent<PoweredLightComponent>(extraLight).CurrentLit, Is.False);
                        Assert.That(SEntMan.GetComponent<PoweredLightComponent>(outsideLight).CurrentLit, Is.True);
                        Assert.That((SEntMan.GetComponent<NetrunnerDisruptionComponent>(device).EndsAt - SGameTiming.CurTime).TotalSeconds,
                            Is.InRange(298, 300));
                    }
                    if (ability.Kind == NetrunnerHackKind.LockAirlock)
                    {
                        Assert.That(SEntMan.GetComponent<DoorComponent>(target).State, Is.EqualTo(DoorState.Closed));
                        Assert.That(SEntMan.System<SharedDoorSystem>().CanOpen(target, user: null), Is.False);
                    }
                    if (ability.Kind == NetrunnerHackKind.OpticalGlitch)
                        Assert.That(SEntMan.GetComponent<BlindableComponent>(target).IsBlind, Is.True);
                    if (ability.Kind == NetrunnerHackKind.LockWeapon)
                    {
                        var shot = new ShotAttemptedEvent { User = user, Used = (device, SEntMan.GetComponent<GunComponent>(device)) };
                        SEntMan.EventBus.RaiseLocalEvent(device, ref shot);
                        Assert.That(shot.Cancelled, Is.True);
                    }
                    break;
            }
        });
        if (ability.Kind == NetrunnerHackKind.DisableLight)
        {
            await Server.WaitPost(() =>
            {
                SEntMan.GetComponent<NetrunnerDisruptionComponent>(device).EndsAt = SGameTiming.CurTime;
                SEntMan.GetComponent<NetrunnerDisruptionComponent>(extraLight).EndsAt = SGameTiming.CurTime;
            });
            await Pair.RunSeconds(0.5f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.GetComponent<PoweredLightComponent>(device).CurrentLit, Is.True);
                Assert.That(SEntMan.GetComponent<PoweredLightComponent>(extraLight).CurrentLit, Is.True);
            });
        }
        if (ability.Kind is NetrunnerHackKind.MotorInterference or NetrunnerHackKind.OpticalGlitch
            or NetrunnerHackKind.LockWeapon or NetrunnerHackKind.LockAirlock)
        {
            await Pair.RunSeconds((float)ability.Duration.TotalSeconds + 0.5f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.HasComponent<NetrunnerDisruptionComponent>(device), Is.False);
                if (ability.Kind == NetrunnerHackKind.OpticalGlitch)
                    Assert.That(SEntMan.GetComponent<BlindableComponent>(target).IsBlind, Is.False);
            });
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ChairConnectsSelectsCameraAndDisconnects(bool instant)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid chair = default;
        EntityUid camera = default;
        EntityUid implant = default;
        NetEntity implantNet = default;
        NetEntity cameraNet = default;
        bool buckled = false;
        TimeSpan connectTime = default;
        BoundUserInterface bui = null;
        var buckle = SEntMan.System<SharedBuckleSystem>();

        await Server.WaitPost(() =>
        {
            var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.EnsureComponent<StationMemberComponent>(map.Grid).Station = station;
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            SEntMan.RemoveComponent<RespiratorComponent>(user);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            implant = SEntMan.System<SharedSubdermalImplantSystem>().AddImplant(user, "FishNetrunnerImplant")!.Value;
            chair = SEntMan.SpawnEntity("FishNetrunnerChair", map.GridCoords);
            connectTime = SEntMan.GetComponent<NetrunnerChairComponent>(chair).ConnectTime;
            SetPowered(chair);
            var cameraCoordinates = new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + Vector2.UnitX);
            SEntMan.System<SharedMapSystem>().SetTile(map.Grid, map.Grid.Comp, cameraCoordinates, map.Tile.Tile);
            camera = SEntMan.SpawnEntity("SurveillanceCameraEngineering", cameraCoordinates);
            SetPowered(camera);
            if (instant)
                SEntMan.System<TagSystem>().AddTag(user, InstantDoAftersTag);
            implantNet = SEntMan.GetNetEntity(implant);
            cameraNet = SEntMan.GetNetEntity(camera);
            buckled = buckle.TryBuckle(user, user, chair);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(buckled, Is.True);
            var resources = SEntMan.GetComponent<NetrunnerImplantComponent>(implant);
            Assert.That(resources.Chair, Is.EqualTo(chair));
            Assert.That(resources.RemoteEye.HasValue, Is.EqualTo(instant));
            Assert.That(resources.Connection.HasValue, Is.EqualTo(!instant));
        });
        await Pair.RunSeconds((float)connectTime.TotalSeconds + 0.5f);
        await Server.WaitAssertion(() =>
        {
            var resources = SEntMan.GetComponent<NetrunnerImplantComponent>(implant);
            Assert.That(resources.Connection, Is.Null);
            Assert.That(resources.RemoteEye, Is.Not.Null);
            Assert.That(SEntMan.GetComponent<RelayInputMoverComponent>(user).RelayEntity, Is.EqualTo(resources.RemoteEye));
            Assert.That(SEntMan.GetComponent<EyeComponent>(user).Target, Is.EqualTo(resources.RemoteEye));
            Assert.That(resources.Chair, Is.EqualTo(chair));
            Assert.That(resources.Ram, Is.EqualTo(resources.MaxRam - SEntMan.GetComponent<NetrunnerChairComponent>(chair).ConnectionCost));
            Assert.That(SEntMan.System<NetrunnerSystem>().CanSelectCamera((implant, resources), user, camera), Is.True);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var ui = Client.System<Robust.Client.GameObjects.UserInterfaceSystem>();
            Assert.That(ui.TryGetOpenUi(CEntMan.GetEntity(implantNet), NetrunnerUiKey.Terminal, out bui), Is.True);
            Assert.That(ui.TryGetUiState(CEntMan.GetEntity(implantNet), NetrunnerUiKey.Terminal, out NetrunnerUiState state), Is.True);
            Assert.That(state.Connected, Is.True);
            Assert.That(state.Cameras.Exists(entry => entry.Entity == cameraNet), Is.True);
        });
        await Client.WaitPost(() => bui.SendMessage(new NetrunnerSelectCameraMessage(cameraNet)));
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<NetrunnerImplantComponent>(implant).Camera, Is.EqualTo(camera));
            Assert.That(ServerSession!.AttachedEntity, Is.EqualTo(user));
        });
        await Server.WaitPost(() => buckle.TryUnbuckle(user, user));
        await Pair.RunUntilSynced();
        await Server.WaitAssertion(() =>
        {
            var resources = SEntMan.GetComponent<NetrunnerImplantComponent>(implant);
            Assert.That(resources.Chair, Is.Null);
            Assert.That(resources.Camera, Is.Null);
            Assert.That(resources.Connection, Is.Null);
            Assert.That(resources.RemoteEye, Is.Null);
            Assert.That(SEntMan.HasComponent<RelayInputMoverComponent>(user), Is.False);
            Assert.That(ServerSession!.AttachedEntity, Is.EqualTo(user));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task MovingWithinRangeKeepsHackAndOutOfRangeCancelsWithShortCooldown(bool remote)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid target = default;
        EntityUid implant = default;
        EntityUid origin = default;
        EntityUid? remoteViewer = null;
        bool buckled = false;
        bool started = false;
        await Server.WaitPost(() =>
        {
            if (remote)
            {
                // Fish-edit: Создаём покрытую камерами плитку до спавна цели, чтобы она принадлежала станции.
                SEntMan.System<SharedMapSystem>().SetTile(map.Grid, map.Grid.Comp,
                    new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + Vector2.UnitX), map.Tile.Tile);
            }
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            target = SEntMan.SpawnEntity("MobHuman",
                new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + Vector2.UnitX));
            SEntMan.RemoveComponent<RespiratorComponent>(user);
            SEntMan.RemoveComponent<RespiratorComponent>(target);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            var implants = SEntMan.System<SharedSubdermalImplantSystem>();
            implant = implants.AddImplant(user, "FishNetrunnerImplant")!.Value;
            SEntMan.System<NetrunnerSystem>().TryBuyProgram(
                (implant, SEntMan.GetComponent<NetrunnerImplantComponent>(implant)), user, NeuralOverload);
            implants.AddImplant(target, "MindShieldImplant");
            origin = user;
            if (remote)
            {
                var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                SEntMan.EnsureComponent<StationMemberComponent>(map.Grid).Station = station;
                var cameraCoordinates = new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + Vector2.UnitX);
                var camera = SEntMan.SpawnEntity("SurveillanceCameraEngineering", cameraCoordinates);
                SetPowered(camera);
                var chair = SEntMan.SpawnEntity("FishNetrunnerChair", map.GridCoords);
                SetPowered(chair);
                SEntMan.System<TagSystem>().AddTag(user, InstantDoAftersTag);
                buckled = SEntMan.System<SharedBuckleSystem>().TryBuckle(user, user, chair);
                SEntMan.System<TagSystem>().RemoveTag(user, InstantDoAftersTag);
                remoteViewer = SEntMan.GetComponent<NetrunnerImplantComponent>(implant).RemoteEye;
                origin = remoteViewer ?? user;
            }
        });
        if (remote)
        {
            Assert.That(buckled, Is.True, "The operator must be buckled before the remote cast.");
            Assert.That(remoteViewer, Is.Not.Null, "The instant chair connection must create a viewer.");
        }
        await Pair.RunSeconds(0.25f);
        await Server.WaitPost(() =>
        {
            started = SEntMan.System<NetrunnerSystem>().TryStartHack(
                (implant, SEntMan.GetComponent<NetrunnerImplantComponent>(implant)), user, target, "FishNetrunnerNeuralOverload");
        });
        await Server.WaitAssertion(() => Assert.That(started, Is.True));
        // Fish-edit: Перемещение в пределах дальности больше не прерывает активацию скрипта.
        await Server.WaitPost(() => SEntMan.System<SharedTransformSystem>().SetCoordinates(origin,
            new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + Vector2.UnitY * 2)));
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            var resources = SEntMan.GetComponent<NetrunnerImplantComponent>(implant);
            Assert.That(resources.Pending, Is.Not.Null, "Movement within range must not cancel the hack.");
        });
        // Fish-edit: Выход за пределы зоны видимости прерывает скрипт и сокращает перезарядку до 4 секунд.
        await Server.WaitPost(() => SEntMan.System<SharedTransformSystem>().SetCoordinates(origin,
            new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + Vector2.UnitY * 14)));
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            var resources = SEntMan.GetComponent<NetrunnerImplantComponent>(implant);
            Assert.That(resources.Pending, Is.Null);
            Assert.That(resources.Journal, Has.Count.EqualTo(1));
            Assert.That(resources.Journal[0].Success, Is.False);
            Assert.That(resources.Ram, Is.EqualTo(resources.MaxRam - 5 - (remote ? 1 : 0)));
            Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.False);
            var remainingCooldown = (resources.Cooldowns[NeuralOverload] - SGameTiming.CurTime).TotalSeconds;
            Assert.That(remainingCooldown, Is.GreaterThan(0).And.LessThanOrEqualTo(4.0));
            var actionPrototype = SProtoMan.Index(NeuralOverload).Action;
            var action = resources.GrantedActions.Find(uid => SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == actionPrototype);
            Assert.That(SEntMan.GetComponent<ActionComponent>(action).Cooldown?.End, Is.EqualTo(resources.Cooldowns[NeuralOverload]));
        });
    }

    [Test]
    public async Task ExtractionPreservesRamTraceAndCooldowns()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid implant = default;
        var implants = SEntMan.System<SharedSubdermalImplantSystem>();
        var containers = SEntMan.System<SharedContainerSystem>();
        var ability = new ProtoId<NetrunnerAbilityPrototype>("FishNetrunnerOpenAirlock");
        TimeSpan cooldown = default;
        await Pair.Server.WaitPost(() =>
        {
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            implant = implants.AddImplant(user, "FishNetrunnerImplant")!.Value;
            SEntMan.System<NetrunnerSystem>().TryBuyProgram(
                (implant, SEntMan.GetComponent<NetrunnerImplantComponent>(implant)), user, ability);
            var resources = SEntMan.GetComponent<NetrunnerImplantComponent>(implant);
            resources.Ram = -2;
            resources.Trace = 61;
            cooldown = SGameTiming.CurTime + TimeSpan.FromMinutes(2);
            resources.Cooldowns[ability] = cooldown;
            containers.Remove(implant, SEntMan.GetComponent<ImplantedComponent>(user).ImplantContainer);
        });
        await Pair.Server.WaitAssertion(() =>
        {
            var resources = SEntMan.GetComponent<NetrunnerImplantComponent>(implant);
            Assert.That(resources.Ram, Is.EqualTo(-2));
            Assert.That(resources.Trace, Is.EqualTo(61));
            Assert.That(resources.User, Is.Null);
            Assert.That(SEntMan.HasComponent<NetrunnerUserComponent>(user), Is.False);
        });
        await Pair.Server.WaitPost(() => implants.ForceImplant(user, implant));
        await Pair.Server.WaitAssertion(() =>
        {
            var resources = SEntMan.GetComponent<NetrunnerImplantComponent>(implant);
            Assert.That(resources.Ram, Is.EqualTo(-2));
            Assert.That(resources.Trace, Is.EqualTo(61));
            Assert.That(resources.Cooldowns[ability], Is.EqualTo(cooldown));
            Assert.That(resources.UnlockedAbilities.Contains(ability), Is.True);
            Assert.That(resources.NeuralLinks, Is.EqualTo(10));
            Assert.That(SEntMan.GetComponent<NetrunnerUserComponent>(user).Implant, Is.EqualTo(implant));
            var actionPrototype = SProtoMan.Index(ability).Action;
            var action = resources.GrantedActions.Find(uid => SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == actionPrototype);
            Assert.That(SEntMan.GetComponent<ActionComponent>(action).Cooldown?.End, Is.EqualTo(cooldown));
        });
    }

    [Test]
    public async Task NeuralOverloadRequiresRealImplantAndExpires()
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        bool fakeAllowed = true;
        bool realAllowed = false;
        bool applied = false;
        var implants = SEntMan.System<SharedSubdermalImplantSystem>();
        var ability = SProtoMan.Index(NeuralOverload);
        await Pair.Server.WaitPost(() =>
        {
            target = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            SEntMan.RemoveComponent<RespiratorComponent>(target);
            var fake = implants.AddImplant(target, "FakeMindShieldImplant")!.Value;
            var check = new NetrunnerValidateHackEvent(target, ability);
            SEntMan.EventBus.RaiseLocalEvent(target, ref check);
            fakeAllowed = check.Allowed;
            implants.ForceRemove(target, fake);
            implants.AddImplant(target, "MindShieldImplant");
            check = new NetrunnerValidateHackEvent(target, ability);
            SEntMan.EventBus.RaiseLocalEvent(target, ref check);
            realAllowed = check.Allowed;
            var apply = new NetrunnerApplyHackEvent(target, target, ability);
            SEntMan.EventBus.RaiseLocalEvent(target, ref apply);
            applied = apply.Applied;
        });
        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(fakeAllowed, Is.False);
            Assert.That(realAllowed, Is.True);
            Assert.That(applied, Is.True);
            Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.True);
            Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.True);
        });
        await Pair.RunSeconds((float)ability.Duration.TotalSeconds + 1);
        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.False);
        });
    }

    private void SetPowered(EntityUid device)
    {
        if (!SEntMan.TryGetComponent<ApcPowerReceiverComponent>(device, out var receiver))
            return;
        // Fish-edit: Проверяем скрипты на запитанном оборудовании без отдельной сети APC.
        SEntMan.System<SharedPowerReceiverSystem>().SetNeedsPower(device, false, receiver);
        receiver.Powered = true;
        var power = new PowerChangedEvent(true, 1000);
        SEntMan.EventBus.RaiseLocalEvent(device, ref power);
    }
}
