#nullable enable
using Content.Server.Electrocution;
using Content.Shared._Fish.Ipc;
using Content.Shared._Fish.Ipc.Modules;
using Content.Shared.Changeling.Components;
using Content.Shared.Changeling.Systems;
using Content.Shared.Forensics.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Nutrition;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.Ipc;

/// <summary>
/// Плейтест автора: еда, скорость, мультитул, ДНК, термалы, шок от сети.
/// </summary>
public sealed class FishIpcPlaytestTest : FishIpcGameTest
{
    private static readonly EntProtoId ThermalAction = "FishActionIpcToggleThermal";
    [Test]
    public async Task Food_IsRefusedAsFood_NotAsMedicine()
    {
        await Server.WaitAssertion(() =>
        {
            var ipc = SSpawn("MobIpc");
            var burger = SSpawn("FoodBurgerBacon");

            var eat = new AttemptIngestEvent(ipc, burger, true);
            SEntMan.EventBus.RaiseLocalEvent(ipc, ref eat);

            Assert.That(eat.Handled, Is.True);
            Assert.That(SEntMan.EntityExists(burger), Is.True);

            var pill = SSpawn("PillDylovene");
            var dose = new AttemptIngestEvent(ipc, pill, true);
            SEntMan.EventBus.RaiseLocalEvent(ipc, ref dose);
            Assert.That(dose.Handled, Is.True);
            Assert.That(SEntMan.EntityExists(pill), Is.True);
        });
    }

    [Test]
    public async Task ChargedChassis_WalksAtHumanSpeed_EmptyDoesNot()
    {
        EntityUid ipc = default;
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            ipc = SSpawn("MobIpc");
            human = SSpawn("MobHuman");
        });

        await RunSeconds(0.5f);

        await Server.WaitAssertion(() =>
        {
            var ipcMove = SComp<MovementSpeedModifierComponent>(ipc);
            var humanMove = SComp<MovementSpeedModifierComponent>(human);
            Assert.That(ipcMove.BaseWalkSpeed, Is.EqualTo(humanMove.BaseWalkSpeed));
            Assert.That(ipcMove.BaseSprintSpeed, Is.EqualTo(humanMove.BaseSprintSpeed));
            Assert.That(ipcMove.WalkSpeedModifier, Is.EqualTo(1f).Within(0.01f));
            Assert.That(ipcMove.SprintSpeedModifier, Is.EqualTo(1f).Within(0.01f));

            var (_, max) = Server.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge(Cell(ipc).Owner);
            SetCellCharge(ipc, max * 0.6f);
        });

        await RunSeconds(0.5f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<MovementSpeedModifierComponent>(ipc).SprintSpeedModifier, Is.EqualTo(1f).Within(0.01f));
            SetCellCharge(ipc, 0f);
        });

        await RunSeconds(0.5f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<FishIpcComponent>(ipc).PowerDown, Is.True);
            Assert.That(SComp<MovementSpeedModifierComponent>(ipc).SprintSpeedModifier, Is.EqualTo(0.2f).Within(0.01f));
        });
    }

    [Test]
    public async Task ToolHand_SpawnsMultitool()
    {
        await Server.WaitAssertion(() =>
        {
            var ipc = SSpawn("MobIpc");
            var module = InstallModule(ipc, "FishIpcModuleToolHand");
            var spawned = SComp<FishIpcToolHandModuleComponent>(module).SpawnedTool;
            Assert.That(spawned, Is.Not.Null);
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(spawned!.Value).EntityPrototype?.ID, Is.EqualTo("Multitool"));
        });
    }

    [Test]
    public async Task DnaInjector_CannotDrawOrInjectIpc()
    {
        await Server.WaitAssertion(() =>
        {
            var ipc = SSpawn("MobIpc");
            var human = SSpawn("MobHuman");
            var injector = SSpawn("DnaInjectorUnlimited");
            var cloner = Server.System<ChangelingClonerSystem>();

            Assert.That(SEntMan.HasComponent<DnaComponent>(ipc), Is.False);
            Assert.That(SEntMan.HasComponent<DnaComponent>(human), Is.True);
            Assert.That(cloner.TryDraw(injector, ipc, human), Is.False);

            var comp = SComp<ChangelingClonerComponent>(injector);
            comp.State = ChangelingClonerState.Filled;
            Assert.That(cloner.TryInject(injector, ipc, human), Is.False);
            Assert.That(comp.State, Is.EqualTo(ChangelingClonerState.Filled));
        });
    }

    [Test]
    public async Task ThermalModule_UsesThermalToggle()
    {
        await Server.WaitAssertion(() =>
        {
            var module = SSpawn("FishIpcModuleNightVision");
            Assert.That(SComp<FishIpcVisionModuleComponent>(module).ToggleAction, Is.EqualTo(ThermalAction));
        });
    }

    [Test]
    public async Task CritChunks_KillIfNotRepaired()
    {
        EntityUid ipc = default;

        await Server.WaitPost(() =>
        {
            DisableDamageVariance();
            ipc = SSpawn("MobIpc");
            Hurt(ipc, "Blunt", 190f);
        });

        await RunSeconds(26f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Damage(ipc, "Blunt"), Is.GreaterThanOrEqualTo(200f));
            Assert.That(SComp<MobStateComponent>(ipc).CurrentState, Is.EqualTo(MobState.Dead));
        });
    }

    [Test]
    public async Task Electrocution_DamagesAndCharges()
    {
        EntityUid ipc = default;
        float before = 0;

        await Server.WaitPost(() =>
        {
            DisableDamageVariance();
            ipc = SSpawn("MobIpc");
            SetCellCharge(ipc, 100f);
            before = CellCharge(ipc);
            Assert.That(Server.System<ElectrocutionSystem>().TryDoElectrocution(
                ipc, ipc, 8, TimeSpan.FromSeconds(0.5f), refresh: true), Is.True);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(Damage(ipc, "Shock"), Is.GreaterThan(0f));
            Assert.That(CellCharge(ipc), Is.GreaterThan(before + 20f));
        });
    }
}
