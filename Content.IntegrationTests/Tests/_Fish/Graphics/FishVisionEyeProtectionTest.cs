using Content.Client._Fish.Graphics.Vision;
using Content.Server.Body.Components;
using Content.Server.Temperature.Systems;
using Content.Shared._Fish.Vision;
using Content.Shared._Sunrise.ThermalVision;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Temperature.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Fish.Graphics;

[TestFixture]
public sealed class FishVisionEyeProtectionTest
{
    private const string ProtectedHuman = "FishVisionProtectedTestHuman";
    private const int NetworkTicks = 20;

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  parent: MobHuman
  id: {ProtectedHuman}
  suffix: Fish
  components:
  - type: EyeProtection
";

    /// <summary>
    /// Body eye protection must not prevent an equipped thermal device from receiving temperatures.
    /// </summary>
    [Test]
    public async Task ThermalDeviceReceivesHeatWithBodyEyeProtection()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        var session = server.PlayerMan.GetSessionById(client.Session!.UserId);
        var previousEntity = session.AttachedEntity;
        EntityUid wearer = default;
        EntityUid target = default;
        NetEntity targetNet = default;
        var equipped = false;
        var activated = false;

        await server.WaitPost(() =>
        {
            wearer = entities.SpawnEntity("MobHuman", map.GridCoords);
            entities.AddComponent<EyeProtectionComponent>(wearer);
            server.PlayerMan.SetAttachedEntity(session, wearer);
            var mask = entities.SpawnEntity("ClothingMaskGasDeathSquadFish", map.GridCoords);
            equipped = server.System<InventorySystem>().TryEquip(wearer, mask, "mask", force: true);
            activated = server.System<ItemToggleSystem>().TryActivate(mask, wearer);

            target = entities.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 2, 0));
            targetNet = entities.GetNetEntity(target);
            entities.RemoveComponent<ThermalRegulatorComponent>(target);
            server.System<MobStateSystem>().ChangeMobState(target, MobState.Dead);
            var temperature = entities.GetComponent<TemperatureComponent>(target);
            server.System<TemperatureSystem>().ChangeHeat((target, temperature),
                (310.15f - temperature.Temperature) * temperature.HeatCapacity, ignoreHeatResistance: true);
        });
        Assert.That(equipped, Is.True);
        Assert.That(activated, Is.True);

        // Ждём серверную выборку температуры и доставку состояния носителю маски.
        await pair.RunTicksSync((int) Math.Ceiling(server.Timing.TickRate * 0.25) + NetworkTicks);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<EyeProtectionComponent>(wearer), Is.True);
            Assert.That(entities.TryGetComponent<FishThermalSignatureComponent>(target, out var signature), Is.True,
                "Eye protection must not prevent a thermal viewer from sampling nearby bodies.");
            Assert.That(entities.CanGetComponentState(signature!, session), Is.True,
                "The protected viewer must also be eligible to receive the sampled temperature.");
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(client.System<FishVisionSystem>().ThermalViewer, Is.Not.Null);
            Assert.That(client.EntMan.GetComponent<FishThermalSignatureComponent>(
                client.EntMan.GetEntity(targetNet)).Temperature, Is.EqualTo(310f).Within(2f));
        });

        await server.WaitPost(() =>
        {
            server.PlayerMan.SetAttachedEntity(session, previousEntity);
            entities.DeleteEntity(map.MapUid);
        });
        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Встроенные термалы используют новую картинку и возвращают ПНВ шлема после отключения.
    /// </summary>
    [TestCase("MobHuman", false)]
    [TestCase(ProtectedHuman, true)]
    public async Task IntrinsicThermalsRestoreHelmetNightVisionWhenDisabled(string wearerPrototype, bool hasEyeProtection)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var session = server.PlayerMan.GetSessionById(client.Session!.UserId);
        var previousEntity = session.AttachedEntity;
        NetEntity wearerNet = default;
        var equipped = false;
        var activated = false;

        await server.WaitPost(() =>
        {
            var wearer = server.EntMan.SpawnEntity(wearerPrototype, map.GridCoords);
            wearerNet = server.EntMan.GetNetEntity(wearer);
            server.PlayerMan.SetAttachedEntity(session, wearer);
            server.EntMan.AddComponent<ThermalVisionComponent>(wearer);
            var helmet = server.EntMan.SpawnEntity("ClothingHeadHelmetCBURN", map.GridCoords);
            equipped = server.System<InventorySystem>().TryEquip(wearer, helmet, "head", force: true);
            activated = server.System<ItemToggleSystem>().TryActivate(helmet, wearer);
        });
        Assert.That(equipped, Is.True);
        Assert.That(activated, Is.True);

        await pair.RunTicksSync(NetworkTicks);
        await client.WaitAssertion(() =>
        {
            var wearer = client.EntMan.GetEntity(wearerNet);
            // Ненетворкаемая защита глаз здесь приходит из прототипа, как у бойцов PlanetWar.
            Assert.That(client.EntMan.HasComponent<EyeProtectionComponent>(wearer), Is.EqualTo(hasEyeProtection));
            Assert.That(client.System<FishVisionSystem>().ThermalViewer, Is.EqualTo(wearer));
            Assert.That(client.System<FishVisionSystem>().NightPresentation.Source, Is.Null);
        });

        await server.WaitPost(() => server.EntMan.RemoveComponent<ThermalVisionComponent>(server.EntMan.GetEntity(wearerNet)));
        await pair.RunTicksSync(NetworkTicks);
        await client.WaitAssertion(() =>
        {
            Assert.That(client.System<FishVisionSystem>().ThermalViewer, Is.Null);
            Assert.That(client.System<FishVisionSystem>().NightPresentation.Source, Is.Not.Null);
        });

        await server.WaitPost(() =>
        {
            server.PlayerMan.SetAttachedEntity(session, previousEntity);
            server.EntMan.DeleteEntity(map.MapUid);
        });
        await pair.CleanReturnAsync();
    }
}
