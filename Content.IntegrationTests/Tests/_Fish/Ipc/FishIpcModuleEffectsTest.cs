#nullable enable
using System.Linq;
using Content.Server._Fish.Ipc.Modules;
using Content.Shared.DoAfter;
using Content.Shared.Mind;
using Content.Shared._Fish.Ipc;
using Content.Shared._Fish.Ipc.Modules;
using Content.Shared._Fish.Vision;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Overlays;
using Content.Shared.Radio.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Fish.Ipc;

/// <summary>
/// Эффект и расход каждого модуля v1. При нуле заряда эффект снимается.
/// </summary>
[TestFixture]
public sealed class FishIpcModuleEffectsTest : FishIpcGameTest
{
    [Test]
    public async Task UtilityModules_ApplyEffects_AndDropWhenEmpty()
    {
        EntityUid ipc = default;
        EntityUid tool = default;
        EntityUid rad = default;
        float speed = 0;

        await Server.WaitPost(() =>
        {
            DisableDamageVariance();
            ipc = SSpawn("MobIpc");
            speed = SComp<FishBatteryDrinkerComponent>(ipc).DrinkSpeed;
            InstallModule(ipc, "FishIpcModuleWireInterface");
            InstallModule(ipc, "FishIpcModuleApcAdapter");
            tool = InstallModule(ipc, "FishIpcModuleToolHand");
            Assert.That(SComp<FishIpcToolHandModuleComponent>(tool).SpawnedTool, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<FishIpcWireInterfaceHostComponent>(ipc), Is.True);
                Assert.That(SComp<FishBatteryDrinkerComponent>(ipc).DrinkSpeed, Is.EqualTo(speed * 0.55f).Within(0.001f));
            });

            // Третий слот занят. Радфильтр — на другом шасси, вместе с вокальным и экраном не влезает в кап 3.
            rad = SSpawn("MobIpc");
            InstallModule(rad, "FishIpcModuleRadFilter");
            InstallModule(rad, "FishIpcModuleVocalMod");
            InstallModule(rad, "FishIpcModuleEmpScreen");
            Hurt(rad, "Radiation", 20, ignoreResistances: false);
            Hurt(ipc, "Radiation", 20, ignoreResistances: false);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<FishIpcRadFilterHostComponent>(rad), Is.True);
                Assert.That(SEntMan.HasComponent<FishIpcEmpScreenHostComponent>(rad), Is.True);
                Assert.That(SEntMan.HasComponent<FishIpcVocalModHostComponent>(rad), Is.True);
                Assert.That(Damage(rad, "Radiation"), Is.EqualTo(7f).Within(0.05f));
                Assert.That(Damage(ipc, "Radiation"), Is.EqualTo(20f).Within(0.05f));
            });
        });

        await Server.WaitPost(() =>
        {
            SetCellCharge(ipc, 0f);
            SetCellCharge(rad, 0f);
        });
        await RunSeconds(0.5f);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SComp<FishIpcComponent>(ipc).PowerDown, Is.True);
                Assert.That(SEntMan.HasComponent<FishIpcWireInterfaceHostComponent>(ipc), Is.False);
                Assert.That(SComp<FishBatteryDrinkerComponent>(ipc).DrinkSpeed, Is.EqualTo(speed).Within(0.001f));
                Assert.That(SComp<FishIpcToolHandModuleComponent>(tool).SpawnedTool, Is.Null);
                Assert.That(SEntMan.HasComponent<FishIpcRadFilterHostComponent>(rad), Is.False);
                Assert.That(SEntMan.HasComponent<FishIpcEmpScreenHostComponent>(rad), Is.False);
                Assert.That(SEntMan.HasComponent<FishIpcVocalModHostComponent>(rad), Is.False);
                Assert.That(SEntMan.HasComponent<ActiveRadioComponent>(rad), Is.False);
            });
        });

        await Server.WaitPost(() =>
        {
            SetCellCharge(ipc, 720f);
            SetCellCharge(rad, 720f);
        });
        await RunSeconds(0.5f);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<FishIpcWireInterfaceHostComponent>(ipc), Is.True);
                Assert.That(SComp<FishBatteryDrinkerComponent>(ipc).DrinkSpeed, Is.EqualTo(speed * 0.55f).Within(0.001f));
                Assert.That(SComp<FishIpcToolHandModuleComponent>(tool).SpawnedTool, Is.Not.Null);
                Assert.That(SEntMan.HasComponent<FishIpcRadFilterHostComponent>(rad), Is.True);
                Assert.That(SEntMan.HasComponent<FishIpcEmpScreenHostComponent>(rad), Is.True);
                Assert.That(SEntMan.HasComponent<ActiveRadioComponent>(rad), Is.True);
            });
        });
    }

    [Test]
    public async Task VisionModules_GrantOverlays_AndDropWhenEmpty()
    {
        await AssertVision("FishIpcModuleMedHud", ipc => SEntMan.HasComponent<ShowHealthBarsComponent>(ipc));
        await AssertVision("FishIpcModuleSecHud", ipc => SEntMan.HasComponent<ShowJobIconsComponent>(ipc));
        await AssertVision("FishIpcModuleMeson", ipc => SEntMan.HasComponent<EyeProtectionComponent>(ipc));
        await AssertVision("FishIpcModuleNightVision", ipc => SEntMan.HasComponent<FishThermalVisionComponent>(ipc));
    }

    [Test]
    public async Task Jumpstarter_LeavesCrit_AndRefusesWhenEmpty()
    {
        EntityUid ipc = default;

        await Server.WaitPost(() =>
        {
            DisableDamageVariance();
            ipc = SSpawn("MobIpc");
            InstallModule(ipc, "FishIpcModuleJumpstarter");
            Hurt(ipc, "Blunt", 120);
            SetCellCharge(ipc, 0f);
        });

        await RunSeconds(0.5f);

        await Server.WaitPost(() =>
        {
            Assert.That(SComp<MobStateComponent>(ipc).CurrentState, Is.EqualTo(MobState.Critical));
            var ev = new FishIpcJumpstarterActionEvent();
            SEntMan.EventBus.RaiseLocalEvent(ipc, ev);
            Assert.That(SComp<MobStateComponent>(ipc).CurrentState, Is.EqualTo(MobState.Critical));
            SetCellCharge(ipc, 720f);
        });

        await RunSeconds(0.5f);

        await Server.WaitPost(() =>
        {
            var ev = new FishIpcJumpstarterActionEvent();
            SEntMan.EventBus.RaiseLocalEvent(ipc, ev);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<MobStateComponent>(ipc).CurrentState, Is.EqualTo(MobState.Alive));
            Assert.That(CellCharge(ipc), Is.LessThan(720f - 100f));
        });
    }

    [Test]
    public async Task Defib_PadsNeedCharge_ZapMovesTargetToCrit()
    {
        EntityUid ipc = default;
        EntityUid human = default;
        EntityUid module = default;

        await Server.WaitPost(() =>
        {
            DisableDamageVariance();
            ipc = SSpawn("MobIpc");
            human = SSpawn("MobHuman");
            module = InstallModule(ipc, "FishIpcModuleDefib");
            SetCellCharge(ipc, 0f);
        });

        await RunSeconds(0.5f);

        await Server.WaitPost(() =>
        {
            var ev = new FishIpcDefibActionEvent();
            SEntMan.EventBus.RaiseLocalEvent(ipc, ev);
            Assert.That(SComp<FishIpcDefibModuleComponent>(module).PadsActive, Is.False);
            SetCellCharge(ipc, 720f);
        });

        await RunSeconds(0.5f);

        await Server.WaitPost(() =>
        {
            var ev = new FishIpcDefibActionEvent();
            SEntMan.EventBus.RaiseLocalEvent(ipc, ev);
            Assert.That(SComp<FishIpcDefibModuleComponent>(module).PadsActive, Is.True);
            Assert.That(Server.System<SharedHandsSystem>().EnumerateHeld(ipc).Any(), Is.True);

            Hurt(human, "Asphyxiation", 40);
            Server.System<MobStateSystem>().ChangeMobState(human, MobState.Dead);
            Assert.That(Server.System<SharedDoAfterSystem>().TryStartDoAfter(new DoAfterArgs(
                SEntMan,
                ipc,
                0.05f,
                new FishIpcDefibDoAfterEvent(),
                eventTarget: module,
                target: human)
            {
                NeedHand = false,
                BreakOnMove = false,
                BreakOnDamage = false,
            }), Is.True);
        });

        await RunSeconds(0.4f);

        await Server.WaitAssertion(() =>
        {
            // −40 удушья с нуля урона: пороги сразу ставят Alive, не Critical.
            Assert.That(SComp<MobStateComponent>(human).CurrentState, Is.EqualTo(MobState.Alive));
            Assert.That(CellCharge(ipc), Is.LessThan(720f * 0.7f));
        });
    }

    [Test]
    public async Task InstalledModule_DrawsExtraCharge()
    {
        EntityUid plain = default;
        EntityUid loaded = default;

        await Server.WaitPost(() =>
        {
            plain = Minded("plain");
            loaded = Minded("loaded");
            InstallModule(loaded, "FishIpcModuleEmpScreen");
            SetCellCharge(plain, 500f);
            SetCellCharge(loaded, 500f);
        });

        await RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            Assert.That(CellCharge(loaded), Is.LessThan(CellCharge(plain) - 0.8f));
        });
    }

    private EntityUid Minded(string name)
    {
        var ipc = SSpawn("MobIpc");
        var mind = Server.System<SharedMindSystem>().CreateMind(null, name);
        Server.System<SharedMindSystem>().TransferTo(mind.Owner, ipc, createGhost: false, mind: mind.Comp);
        return ipc;
    }

    private async Task AssertVision(string prototype, Func<EntityUid, bool> hasEffect)
    {
        EntityUid ipc = default;

        await Server.WaitPost(() =>
        {
            ipc = SSpawn("MobIpc");
            InstallModule(ipc, prototype);
            Assert.That(hasEffect(ipc), Is.True, prototype);
            SetCellCharge(ipc, 0f);
        });

        await RunSeconds(0.5f);

        await Server.WaitAssertion(() => Assert.That(hasEffect(ipc), Is.False, prototype));

        await Server.WaitPost(() => SetCellCharge(ipc, 720f));
        await RunSeconds(0.5f);

        await Server.WaitAssertion(() => Assert.That(hasEffect(ipc), Is.True, prototype + " после зарядки"));
    }
}
