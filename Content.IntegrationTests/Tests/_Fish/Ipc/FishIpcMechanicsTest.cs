#nullable enable
using System.Linq;
using Content.Server._Fish.Ipc;
using Content.Shared._Fish.Ipc;
using Content.Shared._Fish.Ipc.Modules;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Emp;
using Content.Shared.Electrocution;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Lock;
using Content.Shared.Medical;
using Content.Shared.Medical.Healing;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power.Components;
using Content.Shared.Radio.Components;
using Content.Shared.Wires;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Fish.Ipc;

/// <summary>
/// Батарея, зарядка, ремонт инструментами, EMP, шок, крит-урон, перезапуск, панель.
/// </summary>
[TestFixture]
public sealed class FishIpcMechanicsTest : FishIpcGameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: FishIpcTestDrinkSource
  components:
  - type: Battery
    maxCharge: 100000
    startingCharge: 100000
  - type: FishBatteryDrinkerSource
";

    [Test]
    public async Task MindedChassis_DrainsCell_ZeroChargePowersDown_RechargePowersUp()
    {
        EntityUid ipc = default;
        float charged = 0;

        await Server.WaitPost(() =>
        {
            ipc = SSpawn("MobIpc");
            var mind = Server.System<SharedMindSystem>().CreateMind(null, "ipc-drain");
            Server.System<SharedMindSystem>().TransferTo(mind.Owner, ipc, createGhost: false, mind: mind.Comp);
            charged = CellCharge(ipc);
        });

        await RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(CellCharge(ipc), Is.LessThan(charged - 2f));
            Assert.That(SComp<FishIpcComponent>(ipc).PowerDown, Is.False);
        });

        await Server.WaitPost(() => SetCellCharge(ipc, 0f));
        await RunSeconds(0.5f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<FishIpcComponent>(ipc).PowerDown, Is.True);
            Assert.That(SEntMan.HasComponent<ActiveRadioComponent>(ipc), Is.False);
        });

        await Server.WaitPost(() => SetCellCharge(ipc, 720f));
        await RunSeconds(0.5f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<FishIpcComponent>(ipc).PowerDown, Is.False);
            Assert.That(SEntMan.HasComponent<ActiveRadioComponent>(ipc), Is.True);
        });
    }

    [Test]
    public async Task Welding_UnlitRejects_SelfHealIsSlowerThanOther()
    {
        var map = await Pair.CreateTestMap();
        EntityUid ipc = default;
        EntityUid welder = default;
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            DisableDamageVariance();
            ipc = SSpawnAtPosition("MobIpc", map.GridCoords);
            human = SSpawnAtPosition("MobHuman", map.GridCoords);
            welder = SSpawnAtPosition("Welder", map.GridCoords);
            Hurt(ipc, "Blunt", 40);

            var weldable = SComp<FishWeldingHealingComponent>(welder);
            var weld = Server.System<SharedFishWeldingHealableSystem>();
            Assert.That(weld.TryStartWeld((welder, weldable), ipc, ipc), Is.EqualTo(FishWeldResult.Rejected));

            Assert.That(Server.System<ItemToggleSystem>().TrySetActive(welder, true, ipc, predicted: false, showPopup: false), Is.True);
            Assert.That(Server.System<SharedHandsSystem>().TryForcePickupAnyHand(ipc, welder, checkActionBlocker: false), Is.True);
            Assert.That(weld.TryStartWeld((welder, weldable), ipc, ipc), Is.EqualTo(FishWeldResult.Started));
            Assert.That(ActiveDelay(ipc), Is.EqualTo(TimeSpan.FromSeconds(2)).Within(TimeSpan.FromMilliseconds(50)));
        });

        await RunSeconds(2.4f);

        await Server.WaitAssertion(() =>
        {
            // Самосварка 2 с, за 2.4 с успевает один проход: 40 − 3.34.
            Assert.That(Damage(ipc, "Blunt"), Is.EqualTo(36.66f).Within(0.08f));
        });

        await Server.WaitPost(() =>
        {
            var weld = Server.System<SharedFishWeldingHealableSystem>();
            var weldable = SComp<FishWeldingHealingComponent>(welder);
            Server.System<SharedHandsSystem>().TryForcePickupAnyHand(human, welder, checkActionBlocker: false);
            Assert.That(weld.TryStartWeld((welder, weldable), human, ipc), Is.EqualTo(FishWeldResult.Started));
            Assert.That(ActiveDelay(human), Is.EqualTo(TimeSpan.FromSeconds(1)).Within(TimeSpan.FromMilliseconds(50)));
        });
    }

    [Test]
    public async Task CableSkipsBrute_NanopasteHealsIt_BrutepackDoesNot()
    {
        var map = await Pair.CreateTestMap();
        EntityUid ipc = default;
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            DisableDamageVariance();
            ipc = SSpawnAtPosition("MobIpc", map.GridCoords);
            human = SSpawnAtPosition("MobHuman", map.GridCoords);
            Hurt(ipc, "Blunt", 20);
            Hurt(ipc, "Heat", 20);
            // Катушка ещё и кладёт провод по клику. Лечение гоняем тем же do-after, что HealingSystem.
            StartHealingDoAfter(human, ipc, "CableApcStack1");
        });

        await RunSeconds(0.4f);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(Damage(ipc, "Heat"), Is.LessThan(15f));
                Assert.That(Damage(ipc, "Blunt"), Is.EqualTo(20f));
            });
        });

        await Server.WaitPost(() => StartHealingDoAfter(human, ipc, "Nanopaste1"));
        await RunSeconds(0.4f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Damage(ipc, "Blunt"), Is.LessThan(11f));
        });

        float blunt = 0;
        await Server.WaitPost(() =>
        {
            blunt = Damage(ipc, "Blunt");
            var pack = SSpawn("Brutepack1");
            var coords = SEntMan.GetComponent<TransformComponent>(ipc).Coordinates;
            var ev = new AfterInteractEvent(human, pack, ipc, coords, true);
            SEntMan.EventBus.RaiseLocalEvent(pack, ev);
            Assert.That(ev.Handled, Is.False);
        });

        await RunSeconds(0.4f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Damage(ipc, "Blunt"), Is.EqualTo(blunt));
        });
    }

    [Test]
    public async Task EmpScreen_CutsDamageAndDoesNotDisable()
    {
        EntityUid bare = default;
        EntityUid screened = default;
        var bareDisabled = false;
        var screenedDisabled = true;

        await Server.WaitPost(() =>
        {
            DisableDamageVariance();
            bare = SSpawn("MobIpc");
            screened = SSpawn("MobIpc");
            InstallModule(screened, "FishIpcModuleEmpScreen");

            var bareEv = new EmpPulseEvent(10_000f, false, false, TimeSpan.FromSeconds(2), null);
            var screenedEv = bareEv;
            SEntMan.EventBus.RaiseLocalEvent(bare, ref bareEv);
            SEntMan.EventBus.RaiseLocalEvent(screened, ref screenedEv);
            bareDisabled = bareEv.Disabled;
            screenedDisabled = screenedEv.Disabled;
        });

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(bareDisabled, Is.True);
                Assert.That(screenedDisabled, Is.False);
                Assert.That(Damage(screened, "Heat"), Is.EqualTo(Damage(bare, "Heat") * 0.4f).Within(0.05f));
                Assert.That(Damage(screened, "Shock"), Is.EqualTo(Damage(bare, "Shock") * 0.4f).Within(0.05f));
                Assert.That(Damage(bare, "Heat"), Is.GreaterThan(1f));
            });
        });
    }

    [Test]
    public async Task Shock_AddsBatteryCharge()
    {
        EntityUid ipc = default;
        float before = 0;

        await Server.WaitPost(() =>
        {
            ipc = SSpawn("MobIpc");
            SetCellCharge(ipc, 100f);
            before = CellCharge(ipc);
            var ev = new ElectrocutedEvent(ipc, null, 1f);
            SEntMan.EventBus.RaiseLocalEvent(ipc, ev);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(CellCharge(ipc), Is.GreaterThan(before + 30f));
        });
    }

    [Test]
    public async Task CritBrute_TicksOnlyWhileCritical()
    {
        EntityUid ipc = default;
        float alive = 0;

        await Server.WaitPost(() =>
        {
            DisableDamageVariance();
            ipc = SSpawn("MobIpc");
            Hurt(ipc, "Blunt", 20);
            alive = Damage(ipc, "Blunt");
        });

        await RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Damage(ipc, "Blunt"), Is.EqualTo(alive));
        });

        await Server.WaitPost(() => Hurt(ipc, "Blunt", 90));
        await RunSeconds(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<MobStateComponent>(ipc).CurrentState, Is.EqualTo(MobState.Critical));
            Assert.That(Damage(ipc, "Blunt"), Is.EqualTo(110f + 2f).Within(0.15f));
        });
    }

    [Test]
    public async Task Reboot_RevivesBelowCrit_FailsAbove()
    {
        EntityUid ipc = default;
        EntityUid medic = default;

        await Server.WaitPost(() =>
        {
            DisableDamageVariance();
            ipc = SSpawn("MobIpc");
            medic = SSpawn("MobHuman");
            Hurt(ipc, "Blunt", 220);
            Assert.That(StartReboot(medic, ipc), Is.True);
        });

        await RunSeconds(0.3f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<MobStateComponent>(ipc).CurrentState, Is.EqualTo(MobState.Dead));
        });

        await Server.WaitPost(() =>
        {
            Server.System<DamageableSystem>().ClearAllDamage(ipc);
            Hurt(ipc, "Blunt", 40);
            Server.System<MobStateSystem>().ChangeMobState(ipc, MobState.Dead);
            Assert.That(StartReboot(medic, ipc), Is.True);
        });

        await RunSeconds(0.3f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<MobStateComponent>(ipc).CurrentState, Is.EqualTo(MobState.Alive));
        });
    }

    [Test]
    public async Task BorgCharger_RaisesCellCharge()
    {
        var map = await Pair.CreateTestMap();
        EntityUid ipc = default;
        float before = 0;

        await Server.WaitPost(() =>
        {
            var charger = SSpawnAtPosition("BorgCharger", map.GridCoords);
            SComp<ChargerComponent>(charger).Portable = true;
            ipc = SSpawnAtPosition("MobIpc", map.GridCoords);
            SetCellCharge(ipc, 100f);
            before = CellCharge(ipc);
            var container = Server.System<SharedContainerSystem>().GetContainer(charger, "entity_storage");
            Assert.That(Server.System<SharedContainerSystem>().Insert(ipc, container), Is.True);
        });

        await RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(CellCharge(ipc), Is.GreaterThan(before + 40f));
        });
    }

    [Test]
    public async Task ApcDrink_AddsCharge_AdapterShortensDelay()
    {
        EntityUid ipc = default;
        EntityUid source = default;
        float before = 0;

        await Server.WaitPost(() =>
        {
            ipc = SSpawn("MobIpc");
            source = SSpawn("FishIpcTestDrinkSource");
            SetCellCharge(ipc, 100f);
            before = CellCharge(ipc);
            var speed = SComp<FishBatteryDrinkerComponent>(ipc).DrinkSpeed;
            InstallModule(ipc, "FishIpcModuleApcAdapter");
            Assert.That(SComp<FishBatteryDrinkerComponent>(ipc).DrinkSpeed, Is.EqualTo(speed * 0.55f).Within(0.001f));

            Assert.That(Server.System<SharedDoAfterSystem>().TryStartDoAfter(new DoAfterArgs(
                SEntMan,
                ipc,
                0.05f,
                new FishBatteryDrinkerDoAfterEvent(),
                eventTarget: ipc,
                target: source)
            {
                NeedHand = false,
                BreakOnMove = false,
                BreakOnDamage = false,
            }), Is.True);
        });

        await RunSeconds(0.4f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(CellCharge(ipc), Is.GreaterThan(before + 30f));
        });
    }

    [Test]
    public async Task ClosedPanelAndLock_BlockModuleInstall()
    {
        EntityUid ipc = default;
        var closedBlocked = false;
        var openInserted = false;

        await Server.WaitPost(() =>
        {
            ipc = SSpawn("MobIpc");
            Server.System<LockSystem>().Unlock(ipc, ipc);
            var module = SSpawn("FishIpcModuleWireInterface");
            var coords = SEntMan.GetComponent<TransformComponent>(ipc).Coordinates;
            var blocked = new AfterInteractUsingEvent(ipc, module, ipc, coords, true);
            SEntMan.EventBus.RaiseLocalEvent(ipc, blocked);
            closedBlocked = blocked.Handled && !SComp<FishIpcModuleComponent>(module).Installed;

            var panel = SComp<WiresPanelComponent>(ipc);
            Assert.That(Server.System<SharedWiresSystem>().TogglePanel(ipc, panel, true), Is.True);
            Assert.That(panel.Open, Is.True);
            var opened = new AfterInteractUsingEvent(ipc, module, ipc, coords, true);
            SEntMan.EventBus.RaiseLocalEvent(ipc, opened);
            openInserted = SComp<FishIpcModuleComponent>(module).Installed;
        });

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(closedBlocked, Is.True);
                Assert.That(openInserted, Is.True);
            });
        });
    }

    [Test]
    public async Task Ipc_DoesNotSleep()
    {
        EntityUid ipc = default;
        var cancelled = false;

        await Server.WaitPost(() =>
        {
            ipc = SSpawn("MobIpc");
            var ev = new TryingToSleepEvent(ipc);
            SEntMan.EventBus.RaiseLocalEvent(ipc, ref ev);
            cancelled = ev.Cancelled;
        });

        await Server.WaitAssertion(() => Assert.That(cancelled, Is.True));
    }

    private bool StartReboot(EntityUid user, EntityUid ipc)
    {
        // Перезапуск делает живой, мёртвое шасси само do-after не начинает.
        return Server.System<SharedDoAfterSystem>().TryStartDoAfter(new DoAfterArgs(
            SEntMan,
            user,
            0.05f,
            new FishDeadStartupDoAfterEvent(),
            eventTarget: ipc,
            target: ipc)
        {
            NeedHand = false,
            BreakOnMove = false,
            BreakOnDamage = false,
        });
    }

    private void StartHealingDoAfter(EntityUid user, EntityUid target, string itemProto)
    {
        var item = SSpawn(itemProto);
        Assert.That(Server.System<SharedDoAfterSystem>().TryStartDoAfter(new DoAfterArgs(
            SEntMan,
            user,
            0.05f,
            new HealingDoAfterEvent(),
            eventTarget: target,
            target: target,
            used: item)
        {
            NeedHand = false,
            BreakOnMove = false,
            BreakOnDamage = false,
        }), Is.True, itemProto);
    }

    private TimeSpan ActiveDelay(EntityUid user)
    {
        var doAfter = SComp<DoAfterComponent>(user);
        Assert.That(doAfter.DoAfters, Is.Not.Empty);
        return doAfter.DoAfters.Values.First(d => !d.Cancelled && !d.Completed).Args.Delay;
    }

}
