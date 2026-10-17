#nullable enable
using Content.Client._Fish.Graphics.Vision;
using Content.Server.Body.Components;
using Content.Server.Temperature.Systems;
using Content.Shared._Fish.Vision;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Temperature.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Fish.Graphics;

[TestFixture]
public sealed class FishVisionTest
{
    // Запас на доставку серверного состояния и клиентское обновление оверлеев.
    private const int NetworkTicks = 20;

    [TestCase(true, "eyes")]
    [TestCase(false, "head")]
    public async Task TwoActiveNightDevicesKeepRemainingSource(bool headFirst, string removedSlot)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid wearer = default;
        EntityUid eyes = default;
        EntityUid head = default;
        await server.WaitPost(() =>
        {
            wearer = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            eyes = server.EntMan.SpawnEntity("ClothingEyesNVDSyndicate", map.GridCoords);
            head = server.EntMan.SpawnEntity("ClothingEyesNVDHandcrafted", map.GridCoords);
            server.System<InventorySystem>().TryEquip(wearer, eyes, "eyes", force: true);
            server.System<InventorySystem>().TryEquip(wearer, head, "head", force: true);
            server.System<ItemToggleSystem>().TryActivate(headFirst ? head : eyes, wearer);
            server.System<ItemToggleSystem>().TryActivate(headFirst ? eyes : head, wearer);
        });
        await server.WaitAssertion(() => Assert.That(
            server.EntMan.GetComponent<FishNightVisionComponent>(wearer).Color, Is.EqualTo(Color.FromHex("#FF1020"))));
        await server.WaitPost(() => server.System<InventorySystem>().TryUnequip(wearer, removedSlot, force: true));
        await server.WaitAssertion(() =>
        {
            var remaining = server.EntMan.GetComponent<FishNightVisionComponent>(wearer);
            Assert.That(remaining.Color, Is.EqualTo(Color.FromHex(removedSlot == "eyes" ? "#FFDD33" : "#FF1020")));
        });
        await server.WaitPost(() => server.EntMan.DeleteEntity(removedSlot == "eyes" ? head : eyes));
        // Отложенное удаление компонента завершается после обработки событий удаления предмета.
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() => Assert.That(server.EntMan.HasComponent<FishNightVisionComponent>(wearer), Is.False));
        await server.WaitPost(() => server.EntMan.DeleteEntity(map.MapUid));
        await pair.CleanReturnAsync();
    }

    [TestCase("ClothingEyesNVD", "eyes", null, false)]
    [TestCase("ClothingEyesNVDSec", "eyes", null, false)]
    [TestCase("ClothingEyesNVDMedSec", "eyes", null, false)]
    [TestCase("ClothingEyesNVDSyndicate", "eyes", "#FF1020", false)]
    [TestCase("ClothingEyesGlassesNVG", "eyes", "#FF1020", false)]
    [TestCase("ClothingEyesNVDHandcrafted", "head", "#FFDD33", false)]
    [TestCase("SunriseClothingEyesGlassesThermal", "eyes", null, true)]
    [TestCase("ClothingEyesGlassesThermalChameleon", "eyes", null, true)]
    public async Task ExistingGogglesKeepTogglePowerAndEquipmentLifecycle(string prototype, string slot, string? expectedColor, bool thermal)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var session = server.PlayerMan.GetSessionById(client.Session!.UserId);
        var previousEntity = session.AttachedEntity;
        EntityUid wearer = default;
        EntityUid goggles = default;
        var equipped = false;
        var activated = false;
        await server.WaitPost(() =>
        {
            wearer = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            goggles = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            server.PlayerMan.SetAttachedEntity(session, wearer);
            equipped = server.System<InventorySystem>().TryEquip(wearer, goggles, slot, force: true);
        });
        Assert.That(equipped, Is.True);
        await Check(false);
        await server.WaitPost(() => activated = server.System<ItemToggleSystem>().TryActivate(goggles, wearer));
        Assert.That(activated, Is.True);
        await Check(true);

        await server.WaitPost(() => server.System<InventorySystem>().TryUnequip(wearer, slot, force: true));
        await Check(false);
        await server.WaitPost(() =>
        {
            server.System<InventorySystem>().TryEquip(wearer, goggles, slot, force: true);
            activated = server.System<ItemToggleSystem>().TryActivate(goggles, wearer);
        });
        Assert.That(activated, Is.True);
        await Check(true);
        await server.WaitPost(() =>
        {
            if (server.System<PowerCellSystem>().TryGetBatteryFromSlot(goggles, out var battery))
                server.System<SharedBatterySystem>().SetCharge(battery.Value.AsNullable(), 0f);
        });
        await Check(false);
        await server.WaitPost(() => activated = server.System<ItemToggleSystem>().TryActivate(goggles, wearer));
        Assert.That(activated, Is.False);
        await server.WaitPost(() =>
        {
            server.PlayerMan.SetAttachedEntity(session, previousEntity);
            server.EntMan.DeleteEntity(map.MapUid);
        });
        await pair.CleanReturnAsync();

        async Task Check(bool expected)
        {
            // Ждём доставку включения, снятия или разрядки и применения состояния на клиенте.
            await pair.RunTicksSync(NetworkTicks);
            await server.WaitAssertion(() =>
            {
                Assert.That(thermal ? server.EntMan.HasComponent<FishThermalVisionComponent>(wearer) :
                    server.EntMan.HasComponent<FishNightVisionComponent>(wearer), Is.EqualTo(expected));
            });
            await client.WaitAssertion(() =>
            {
                var vision = client.System<FishVisionSystem>();
                Assert.That(thermal ? vision.ThermalViewer != null : vision.NightPresentation.Source != null,
                    Is.EqualTo(expected));
                if (expected && !thermal)
                {
                    var color = vision.NightPresentation.Color;
                    if (expectedColor != null)
                        Assert.That(color, Is.EqualTo(Color.FromHex(expectedColor)));
                    else
                        Assert.That(color.G, Is.GreaterThan(color.R));
                }
            });
        }
    }

    [Test]
    public async Task TemperatureSurvivesDeathChangesWithCoolingAndDoesNotLoadDistantTargets()
    {
        // Меняем NetPVS, поэтому паре нужен полный recycle перед следующим тестом.
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var session = server.PlayerMan.GetSessionById(client.Session!.UserId);
        EntityUid target = default;
        EntityUid far = default;
        NetEntity targetNet = default;
        NetEntity farNet = default;
        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(Robust.Shared.CVars.NetPVS, true);
            var wearer = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            target = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 2, 0));
            far = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 1000, 0));
            targetNet = server.EntMan.GetNetEntity(target);
            farNet = server.EntMan.GetNetEntity(far);
            server.PlayerMan.SetAttachedEntity(session, wearer);
            server.EntMan.AddComponent<FishThermalVisionComponent>(wearer);
            server.System<MobStateSystem>().ChangeMobState(target, MobState.Dead);
            // Изолируем передачу температуры от охлаждения тела атмосферой тестовой карты.
            server.EntMan.GetComponent<TemperatureComponent>(target).AtmosTemperatureTransferEfficiency = 0f;
            server.System<TemperatureSystem>().ForceChangeTemperature(target, 310.15f);
        });
        await CheckTemperature(310f);
        await server.WaitPost(() => server.System<TemperatureSystem>().ForceChangeTemperature(target, 273.15f));
        await CheckTemperature(273f);
        await server.WaitPost(() => server.EntMan.RemoveComponent<TemperatureComponent>(target));
        // Доставляем удаление сигнатуры через обычный PVS.
        await pair.RunTicksSync(NetworkTicks);
        await client.WaitAssertion(() => Assert.That(
            client.EntMan.HasComponent<FishThermalSignatureComponent>(client.EntMan.GetEntity(targetNet)), Is.False));
        await pair.CleanReturnAsync();

        async Task CheckTemperature(float expected)
        {
            // Полный интервал выборки 0,25 с плюс запас на доставку PVS при текущей частоте тиков.
            await pair.RunTicksSync((int) Math.Ceiling(server.Timing.TickRate * 0.25) + NetworkTicks);
            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.GetComponent<FishThermalSignatureComponent>(target).Temperature,
                    Is.EqualTo(expected).Within(2f));
                Assert.That(server.EntMan.HasComponent<FishThermalSignatureComponent>(far), Is.False);
            });
            await client.WaitAssertion(() =>
            {
                Assert.That(client.EntMan.GetComponent<FishThermalSignatureComponent>(client.EntMan.GetEntity(targetNet)).Temperature,
                    Is.EqualTo(expected).Within(2f));
                Assert.That(client.EntMan.TryGetEntity(farNet, out _), Is.False);
            });
        }
    }

    [Test]
    public async Task TemperatureIsRestrictedToEligibleSessionsAndExpiresWithoutViewers()
    {
        // Дополнительная сессия и изменение NetPVS требуют полного recycle пары.
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var session = server.PlayerMan.GetSessionById(client.Session!.UserId);
        var thermalSession = await server.AddDummySession();
        EntityUid wearer = default;
        EntityUid otherWearer = default;
        EntityUid target = default;
        NetEntity targetNet = default;
        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(Robust.Shared.CVars.NetPVS, true);
            wearer = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            otherWearer = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            target = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 2, 0));
            targetNet = server.EntMan.GetNetEntity(target);
            server.PlayerMan.SetAttachedEntity(session, wearer);
            server.PlayerMan.SetAttachedEntity(thermalSession, otherWearer);
            server.EntMan.AddComponent<FishThermalVisionComponent>(otherWearer);
            // Терморегуляция живого тела не должна менять контрольную температуру между выборками.
            server.EntMan.RemoveComponent<ThermalRegulatorComponent>(target);
            server.System<MobStateSystem>().ChangeMobState(target, MobState.Dead);
            server.EntMan.GetComponent<TemperatureComponent>(target).AtmosTemperatureTransferEfficiency = 0f;
            server.System<TemperatureSystem>().ForceChangeTemperature(target, 310.15f);
        });
        await WaitForSample();
        await client.WaitAssertion(() =>
        {
            Assert.That(client.EntMan.TryGetEntity(targetNet, out var visibleTarget), Is.True);
            Assert.That(client.EntMan.HasComponent<FishThermalSignatureComponent>(visibleTarget), Is.False,
                "An unequipped client must not receive temperatures sampled for another player.");
        });
        await server.WaitAssertion(() =>
        {
            var signature = server.EntMan.GetComponent<FishThermalSignatureComponent>(target);
            Assert.That(signature.SessionSpecific, Is.True);
            Assert.That(server.EntMan.CanGetComponentState(signature, thermalSession), Is.True);
            Assert.That(server.EntMan.CanGetComponentState(signature, session), Is.False);
        });

        await server.WaitPost(() => server.EntMan.AddComponent<FishThermalVisionComponent>(wearer));
        await WaitForSample();
        await AssertClientTemperature(310f);
        await server.WaitPost(() =>
        {
            server.EntMan.RemoveComponent<FishThermalVisionComponent>(wearer);
            server.System<TemperatureSystem>().ForceChangeTemperature(target, 273.15f);
        });
        await WaitForSample();
        // Уже полученные сведения нельзя отозвать из памяти клиента, но новые значения ему недоступны.
        await AssertNoNewClientTemperature(310f);

        await server.WaitPost(() => server.EntMan.AddComponent<FishThermalVisionComponent>(wearer));
        await WaitForSample();
        await AssertClientTemperature(273f);
        await server.WaitPost(() =>
        {
            var vision = server.EntMan.GetComponent<FishThermalVisionComponent>(wearer);
            vision.Range = 0.5f;
            server.EntMan.Dirty(wearer, vision);
            server.System<TemperatureSystem>().ForceChangeTemperature(target, 320.15f);
        });
        await WaitForSample();
        await AssertNoNewClientTemperature(273f);
        await server.WaitPost(() => server.EntMan.RemoveComponent<FishThermalVisionComponent>(otherWearer));
        await WaitForSample();
        await server.WaitAssertion(() => Assert.That(server.EntMan.HasComponent<FishThermalSignatureComponent>(target), Is.False));
        await client.WaitAssertion(() => Assert.That(
            client.EntMan.HasComponent<FishThermalSignatureComponent>(client.EntMan.GetEntity(targetNet)), Is.False));
        await pair.CleanReturnAsync();

        async Task WaitForSample()
        {
            // Ждём интервал серверной выборки и доставку компонентного состояния через PVS.
            await pair.RunTicksSync((int) Math.Ceiling(server.Timing.TickRate * 0.25) + NetworkTicks);
        }

        async Task AssertClientTemperature(float expected)
        {
            await client.WaitAssertion(() => Assert.That(
                client.EntMan.GetComponent<FishThermalSignatureComponent>(client.EntMan.GetEntity(targetNet)).Temperature,
                Is.EqualTo(expected)));
        }

        async Task AssertNoNewClientTemperature(float lastReceived)
        {
            await server.WaitAssertion(() => Assert.That(server.EntMan.CanGetComponentState(
                server.EntMan.GetComponent<FishThermalSignatureComponent>(target), session), Is.False));
            await client.WaitAssertion(() =>
            {
                if (client.EntMan.TryGetComponent<FishThermalSignatureComponent>(client.EntMan.GetEntity(targetNet), out var signature))
                    Assert.That(signature.Temperature, Is.EqualTo(lastReceived));
            });
        }
    }
}
