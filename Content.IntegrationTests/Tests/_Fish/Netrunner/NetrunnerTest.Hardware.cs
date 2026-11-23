using System.Numerics;
using Content.Server._Fish.Netrunner;
using Content.Server.Body.Components;
using Content.Shared._Fish.Netrunner;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Implants;
using Content.Shared.Inventory;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Tag;
using Content.Shared.Temperature.Components;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.Netrunner;

public sealed partial class NetrunnerTest
{
    private static readonly ProtoId<NetrunnerAbilityPrototype> BatteryDrainAbility = "FishNetrunnerBatteryDrain";
    private static readonly ProtoId<NetrunnerAbilityPrototype> OpenAirlockAbility = "FishNetrunnerOpenAirlock";
    [Test]
    public async Task DebugImplantRunsRepeatedClientActionsAndPurchasesForFree()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid implant = default;
        EntityUid gun = default;
        EntityUid battery = default;
        NetEntity actionNet = default;
        NetEntity targetNet = default;
        NetEntity implantNet = default;
        bool memoryPurchased = false;
        bool speedPurchased = false;
        bool visorPurchased = false;
        float initialCharge = 0;
        var ability = SProtoMan.Index(BatteryDrainAbility);
        await Server.WaitPost(() =>
        {
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            SEntMan.RemoveComponent<RespiratorComponent>(user);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            implant = SEntMan.System<SharedSubdermalImplantSystem>().AddImplant(user, "FishNetrunnerImplantDebug")!.Value;
            implantNet = SEntMan.GetNetEntity(implant);
            var resources = SEntMan.GetComponent<NetrunnerImplantComponent>(implant);
            resources.NeuralLinks = 0;
            var system = SEntMan.System<NetrunnerSystem>();
            memoryPurchased = system.TryBuyUpgrade((implant, resources), user, NetrunnerUpgrade.Memory);
            speedPurchased = system.TryBuyUpgrade((implant, resources), user, NetrunnerUpgrade.ActivationSpeed);
            visorPurchased = system.TryBuyVisor((implant, resources), user);
            system.TryOpenShop((implant, resources), user);
            gun = SEntMan.SpawnEntity("WeaponLaserCarbine",
                new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + Vector2.UnitX));
            SEntMan.System<PowerCellSystem>().TryGetBatteryFromSlotOrEntity((gun, null), out var cell);
            battery = cell!.Value.Owner;
            initialCharge = SEntMan.System<SharedBatterySystem>().GetCharge(battery).Charge;
            foreach (var action in resources.GrantedActions)
            {
                if (SEntMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID == ability.Action)
                    actionNet = SEntMan.GetNetEntity(action);
            }
            targetNet = SEntMan.GetNetEntity(gun);
        });
        await Server.WaitAssertion(() =>
        {
            var resources = SEntMan.GetComponent<NetrunnerImplantComponent>(implant);
            Assert.That(memoryPurchased && speedPurchased && visorPurchased, Is.True);
            Assert.That(resources.NeuralLinks, Is.Zero);
            Assert.That(resources.Ram, Is.EqualTo(resources.MaxRam));
            Assert.That(resources.UnlockedAbilities, Has.Count.EqualTo(16));
            foreach (var action in resources.GrantedActions)
                Assert.That(SEntMan.GetComponent<ActionComponent>(action).UseDelay, Is.EqualTo(TimeSpan.Zero));
            Assert.That(SEntMan.System<NetrunnerSystem>().GetCastTime((implant, resources), ability), Is.EqualTo(TimeSpan.Zero));
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var ui = Client.System<Robust.Client.GameObjects.UserInterfaceSystem>();
            Assert.That(ui.TryGetUiState(CEntMan.GetEntity(implantNet), NetrunnerUiKey.Shop, out NetrunnerShopState state), Is.True);
            Assert.That(state.DebugMode && state.CanBuy, Is.True);
        });
        for (var i = 0; i < 2; i++)
        {
            await Client.WaitPost(() => CEntMan.RaisePredictiveEvent(new RequestPerformActionEvent(actionNet, targetNet)));
            await Pair.RunSeconds(0.25f);
        }
        await Server.WaitAssertion(() =>
        {
            var resources = SEntMan.GetComponent<NetrunnerImplantComponent>(implant);
            Assert.That(resources.Pending, Is.Null);
            Assert.That(resources.Journal, Has.Count.EqualTo(2));
            Assert.That(resources.Journal.TrueForAll(entry => entry.Success), Is.True);
            Assert.That(resources.Ram, Is.EqualTo(resources.MaxRam));
            Assert.That(resources.Trace, Is.Zero);
            Assert.That(resources.ReadyAt, Is.LessThanOrEqualTo(SGameTiming.CurTime));
            var charge = SEntMan.System<SharedBatterySystem>().GetCharge(battery);
            Assert.That(charge.Charge, Is.EqualTo(initialCharge - charge.MaxCharge * 0.6f).Within(0.01f));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task WornVisorReducesCooldownAndDoublesBodyHeating(bool wearVisor)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid implant = default;
        bool started = false;
        bool purchased = false;
        bool duplicateAccepted = true;
        bool equipped = false;
        float heat = 0;
        double cooldown = 0;
        var ability = SProtoMan.Index(OpenAirlockAbility);
        await Server.WaitPost(() =>
        {
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            SEntMan.RemoveComponent<RespiratorComponent>(user);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            implant = SEntMan.System<SharedSubdermalImplantSystem>().AddImplant(user, "FishNetrunnerImplant")!.Value;
            var resources = SEntMan.GetComponent<NetrunnerImplantComponent>(implant);
            var system = SEntMan.System<NetrunnerSystem>();
            system.TryBuyProgram((implant, resources), user, ability.ID);
            if (wearVisor)
            {
                resources.NeuralLinks = 19;
                purchased = system.TryBuyVisor((implant, resources), user);
                duplicateAccepted = system.TryBuyVisor((implant, resources), user);
                if (SEntMan.System<SharedHandsSystem>().TryGetActiveItem((user, null), out var visor))
                    equipped = SEntMan.System<InventorySystem>().TryEquip(user, visor.Value, "eyes");
            }
            var door = SEntMan.SpawnEntity("Airlock",
                new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + Vector2.UnitX));
            SetPowered(door);
            SEntMan.System<TagSystem>().AddTag(user, InstantDoAftersTag);
            var body = SEntMan.GetComponent<TemperatureComponent>(user);
            var before = body.Temperature;
            started = system.TryStartHack((implant, resources), user, door, ability.ID);
            heat = body.Temperature - before;
            cooldown = (resources.Cooldowns[ability.ID] - SGameTiming.CurTime).TotalSeconds;
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(started, Is.True);
            if (wearVisor)
            {
                Assert.That(purchased && equipped, Is.True);
                Assert.That(duplicateAccepted, Is.False);
                Assert.That(SEntMan.GetComponent<NetrunnerImplantComponent>(implant).NeuralLinks, Is.EqualTo(1));
            }
            Assert.That(heat, Is.EqualTo(ability.RamCost * 0.135f * (wearVisor ? 2 : 1)).Within(0.001f));
            Assert.That(cooldown, Is.EqualTo(ability.Cooldown.TotalSeconds / (wearVisor ? 1.5 : 1)).Within(0.001));
            Assert.That(Robust.Shared.Localization.Loc.GetString("fish-netrunner-manual-content"),
                Is.Not.EqualTo("fish-netrunner-manual-content"));
        });
        if (wearVisor)
        {
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
                Assert.That(Client.System<Content.Client._Fish.Netrunner.ShowNetrunnerMindShieldIconsSystem>().IsActive, Is.True));
        }
    }

    [Test]
    public async Task AirlockAuditExpiresEachRecordAfterTenMinutes()
    {
        var map = await Pair.CreateTestMap();
        EntityUid door = default;
        EntityUid implant = default;
        bool started = false;
        await Server.WaitPost(() =>
        {
            var user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            implant = SEntMan.System<SharedSubdermalImplantSystem>().AddImplant(user, "FishNetrunnerImplantDebug")!.Value;
            door = SEntMan.SpawnEntity("Airlock",
                new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + Vector2.UnitX));
            SetPowered(door);
            started = SEntMan.System<NetrunnerSystem>().TryStartHack(
                (implant, SEntMan.GetComponent<NetrunnerImplantComponent>(implant)), user, door, "FishNetrunnerOpenAirlock");
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(started, Is.True);
            Assert.That(SEntMan.GetComponent<NetrunnerAuditComponent>(door).Retention, Is.EqualTo(TimeSpan.FromMinutes(10)));
        });
        await Server.WaitPost(() =>
        {
            var audit = SEntMan.GetComponent<NetrunnerAuditComponent>(door);
            audit.Entries.Insert(0, new NetrunnerLogEntry(SGameTiming.CurTime - TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1),
                NetrunnerHackKind.OpenAirlock, "Airlock", true));
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<NetrunnerAuditComponent>(door).Entries, Has.Count.EqualTo(1)));
        await Server.WaitPost(() =>
        {
            var entries = SEntMan.GetComponent<NetrunnerAuditComponent>(door).Entries;
            entries[0] = entries[0] with { Time = SGameTiming.CurTime - TimeSpan.FromMinutes(10) };
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<NetrunnerAuditComponent>(door), Is.False);
            Assert.That(SEntMan.GetComponent<NetrunnerImplantComponent>(implant).Journal, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task CoolantPackCoolsDownToTwentyCelsiusWithoutWarming()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid hotPack = default;
        EntityUid coldPack = default;
        bool warmApplied = false;
        bool coldApplied = true;
        await Server.WaitPost(() =>
        {
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            SEntMan.RemoveComponent<RespiratorComponent>(user);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            var temp = SEntMan.GetComponent<TemperatureComponent>(user);
            SEntMan.System<Content.Shared.Temperature.Systems.SharedTemperatureSystem>().ChangeHeat(
                (user, temp), (330f - temp.Temperature) * temp.HeatCapacity, ignoreHeatResistance: true);
            hotPack = SEntMan.SpawnEntity("FishCoolantPack1", map.GridCoords);
            SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(user, hotPack);
            warmApplied = SEntMan.System<NetrunnerCoolantSystem>().TryApplyCoolant(
                (hotPack, SEntMan.GetComponent<NetrunnerCoolantPackComponent>(hotPack)), user, user);
        });
        await Pair.RunSeconds(3.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(warmApplied, Is.True);
            Assert.That(SEntMan.GetComponent<TemperatureComponent>(user).Temperature, Is.EqualTo(293.15f).Within(1.0f));
        });
        await Server.WaitPost(() =>
        {
            var temp = SEntMan.GetComponent<TemperatureComponent>(user);
            SEntMan.System<Content.Shared.Temperature.Systems.SharedTemperatureSystem>().ChangeHeat(
                (user, temp), (280f - temp.Temperature) * temp.HeatCapacity, ignoreHeatResistance: true);
            coldPack = SEntMan.SpawnEntity("FishCoolantPack1", map.GridCoords);
            SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(user, coldPack);
            coldApplied = SEntMan.System<NetrunnerCoolantSystem>().TryApplyCoolant(
                (coldPack, SEntMan.GetComponent<NetrunnerCoolantPackComponent>(coldPack)), user, user);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(coldApplied, Is.False);
            Assert.That(SEntMan.GetComponent<TemperatureComponent>(user).Temperature, Is.EqualTo(280f).Within(0.1f));
        });
    }
}
