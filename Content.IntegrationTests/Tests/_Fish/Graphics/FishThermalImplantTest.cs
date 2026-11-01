using System.Linq;
using Content.Client._Fish.Graphics.Vision;
using Content.Shared._Fish.Vision;
using Content.Shared._Sunrise.ThermalVision;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Starlight.Medical.Surgery.Events;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Fish.Graphics;

[TestFixture]
public sealed class FishThermalImplantTest
{
    private const int NetworkTicks = 20;
    private const float GogglesRange = 4f;
    private const string ProtectedHuman = "FishThermalImplantProtectedHuman";
    private const string Goggles = "FishThermalImplantTestGoggles";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  parent: MobHuman
  id: {ProtectedHuman}
  suffix: Fish
  components:
  - type: EyeProtection

- type: entity
  parent: SunriseClothingEyesGlassesThermal
  id: {Goggles}
  suffix: Fish
  components:
  - type: ComponentToggler
    parent: true
    components:
    - type: FishThermalVision
      range: 4
";

    [TestCase("MobHuman", false)]
    [TestCase(ProtectedHuman, true)]
    public async Task CyberEyesUseThermalRenderingAcrossTogglesEquipmentAndExtraction(string patientPrototype, bool eyeProtection)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        var session = server.PlayerMan.GetSessionById(client.Session!.UserId);
        var previousEntity = session.AttachedEntity;
        EntityUid patient = default;
        EntityUid head = default;
        EntityUid scalpel = default;
        EntityUid removeStep = default;
        EntityUid action = default;
        EntityUid goggles = default;
        var cancelled = false;
        await server.WaitPost(() =>
        {
            patient = entities.SpawnEntity(patientPrototype, map.GridCoords);
            server.PlayerMan.SetAttachedEntity(session, patient);
            head = entities.GetComponent<BodyComponent>(patient).Organs!.ContainedEntities.Single(uid =>
                entities.GetComponent<OrganComponent>(uid).Category?.Id == "Head");
            scalpel = entities.SpawnEntity("Scalpel", map.GridCoords);
            removeStep = entities.SpawnEntity("SurgeryStepRemoveEyes", map.GridCoords);
            var remove = new SurgeryStepEvent(patient, patient, head, [scalpel])
            {
                SurgeryProto = "SurgeryExtractEyes", StepProto = "SurgeryStepRemoveEyes",
            };
            entities.EventBus.RaiseLocalEvent(removeStep, ref remove);
            var eyes = entities.SpawnEntity("CyberEyeThermal", map.GridCoords);
            var insertStep = entities.SpawnEntity("SurgeryStepInsertEyes", map.GridCoords);
            var insert = new SurgeryStepEvent(patient, patient, head, [eyes])
            {
                SurgeryProto = "SurgeryImplantEyes", StepProto = "SurgeryStepInsertEyes",
            };
            entities.EventBus.RaiseLocalEvent(insertStep, ref insert);
            cancelled = remove.IsCancelled || insert.IsCancelled;
        });
        Assert.That(cancelled, Is.False);
        await server.WaitAssertion(() =>
        {
            var toggle = entities.GetComponent<ToggleableThermalVisionComponent>(patient);
            Assert.That(toggle.Active, Is.False);
            Assert.That(toggle.ActionEntity, Is.Not.Null);
        });
        await server.WaitPost(() => action = entities.GetComponent<ToggleableThermalVisionComponent>(patient).ActionEntity!.Value);
        await CheckVision(false);
        await ToggleImplant();
        await CheckVision(true);
        await ToggleImplant();
        await CheckVision(false);
        await ToggleImplant();
        await CheckVision(true);

        var equipped = false;
        var activated = false;
        await server.WaitPost(() =>
        {
            goggles = entities.SpawnEntity(Goggles, map.GridCoords);
            equipped = server.System<InventorySystem>().TryEquip(patient, goggles, "eyes", force: true);
            activated = server.System<ItemToggleSystem>().TryActivate(goggles, patient);
        });
        Assert.That(equipped, Is.True);
        Assert.That(activated, Is.True);
        await CheckVision(true, GogglesRange);
        await ToggleImplant();
        await CheckVision(true, GogglesRange);
        await ToggleImplant();
        await CheckVision(true, GogglesRange);
        await server.WaitPost(() => server.System<ItemToggleSystem>().TryDeactivate(goggles, patient));
        await CheckVision(true);
        await server.WaitPost(() => server.System<ItemToggleSystem>().TryActivate(goggles, patient));
        await CheckVision(true, GogglesRange);
        await server.WaitPost(() => server.System<InventorySystem>().TryUnequip(patient, "eyes", force: true));
        await CheckVision(true);
        await server.WaitPost(() =>
        {
            server.System<InventorySystem>().TryEquip(patient, goggles, "eyes", force: true);
            server.System<ItemToggleSystem>().TryActivate(goggles, patient);
        });
        await CheckVision(true, GogglesRange);

        await server.WaitPost(() =>
        {
            var remove = new SurgeryStepEvent(patient, patient, head, [scalpel])
            {
                SurgeryProto = "SurgeryExtractEyes", StepProto = "SurgeryStepRemoveEyes",
            };
            entities.EventBus.RaiseLocalEvent(removeStep, ref remove);
            cancelled = remove.IsCancelled;
        });
        Assert.That(cancelled, Is.False);
        await CheckVision(true, GogglesRange);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<ToggleableThermalVisionComponent>(patient), Is.False);
            Assert.That(entities.HasComponent<ThermalVisionComponent>(patient), Is.False);
            Assert.That(entities.GetComponent<ActionsComponent>(patient).Actions, Does.Not.Contain(action));
        });
        await server.WaitPost(() => server.System<InventorySystem>().TryUnequip(patient, "eyes", force: true));
        await CheckVision(false);
        await server.WaitPost(() =>
        {
            server.PlayerMan.SetAttachedEntity(session, previousEntity);
            entities.DeleteEntity(map.MapUid);
        });
        await pair.CleanReturnAsync();

        async Task ToggleImplant() => await server.WaitPost(() =>
        {
            var actions = server.System<SharedActionsSystem>();
            actions.PerformAction(patient, actions.GetAction(action)!.Value, predicted: false);
        });

        async Task CheckVision(bool active, float range = 16f)
        {
            await pair.RunTicksSync(NetworkTicks);
            await server.WaitAssertion(() =>
            {
                Assert.That(entities.HasComponent<FishThermalVisionComponent>(patient), Is.EqualTo(active));
                if (active)
                    Assert.That(entities.GetComponent<FishThermalVisionComponent>(patient).Range, Is.EqualTo(range));
            });
            await client.WaitAssertion(() =>
            {
                var viewer = client.System<FishVisionSystem>().ThermalViewer;
                Assert.That(viewer != null, Is.EqualTo(active));
                if (viewer is { } uid)
                {
                    Assert.That(client.EntMan.HasComponent<EyeProtectionComponent>(uid), Is.EqualTo(eyeProtection));
                    Assert.That(client.System<FishVisionSystem>().ThermalRange, Is.EqualTo(range));
                }
            });
        }
    }
}
