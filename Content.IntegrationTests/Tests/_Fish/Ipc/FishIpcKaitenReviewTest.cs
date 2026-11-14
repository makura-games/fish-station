#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared._Fish.Ipc;
using Content.Shared._Fish.Ipc.Modules;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Electrocution;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Lock;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Fish.Ipc;

/// <summary>Замечания Kaiten: сварка, перезапуск, дефиб, зрение, do-after.</summary>
public sealed class FishIpcKaitenReviewTest : FishIpcGameTest
{
    [Test]
    public async Task Welder_WithoutEnoughFuel_DoesNotStart_AndUseInHandStillToggles()
    {
        var map = await Pair.CreateTestMap();
        EntityUid ipc = default;
        EntityUid welder = default;

        await Server.WaitPost(() =>
        {
            DisableDamageVariance();
            ipc = SSpawnAtPosition("MobIpc", map.GridCoords);
            welder = SSpawnAtPosition("Welder", map.GridCoords);
            Hurt(ipc, "Blunt", 30);
            Assert.That(Server.System<ItemToggleSystem>().TrySetActive(welder, true, ipc, predicted: false, showPopup: false), Is.True);

            var solutions = Server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(welder, "welder", out var solution), Is.True);
            solutions.RemoveReagent(solution!.Value, "WeldingFuel", FixedPoint2.New(1000));

            var healing = SComp<FishWeldingHealingComponent>(welder);
            Assert.That(Server.System<SharedFishWeldingHealableSystem>().TryStartWeld((welder, healing), ipc, ipc),
                Is.EqualTo(FishWeldResult.Rejected));

            Server.System<SharedHandsSystem>().TryForcePickupAnyHand(ipc, welder, checkActionBlocker: false);
            SEntMan.EventBus.RaiseLocalEvent(welder, new UseInHandEvent(ipc));
            Assert.That(SComp<ItemToggleComponent>(welder).Activated, Is.False);
        });
    }

    [Test]
    public async Task Reboot_BreaksWhenBodyMoves()
    {
        var map = await Pair.CreateTestMap();
        EntityUid ipc = default;
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            ipc = SSpawnAtPosition("MobIpc", map.GridCoords);
            human = SSpawnAtPosition("MobHuman", map.GridCoords);
            Hurt(ipc, "Blunt", 40);
            Server.System<MobStateSystem>().ChangeMobState(ipc, MobState.Dead);
            SComp<FishDeadStartupButtonComponent>(ipc).DoAfterDelay = TimeSpan.FromSeconds(0.4f);

            var verbs = new GetVerbsEvent<AlternativeVerb>(
                human, ipc, null, SComp<HandsComponent>(human), true, true, true, new List<VerbCategory>());
            SEntMan.EventBus.RaiseLocalEvent(ipc, verbs);
            Assert.That(verbs.Verbs, Is.Not.Empty);
            verbs.Verbs.First().Act!.Invoke();

            var xform = SComp<TransformComponent>(ipc);
            xform.Coordinates = xform.Coordinates.Offset(new Vector2(6, 0));
        });

        await RunSeconds(1);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<MobStateComponent>(ipc).CurrentState, Is.EqualTo(MobState.Dead));
        });
    }

    [Test]
    public async Task Defib_DoesNotStunLiving_AndNeedsTwoHands()
    {
        var map = await Pair.CreateTestMap();
        EntityUid ipc = default;
        EntityUid patient = default;

        await Server.WaitPost(() =>
        {
            ipc = SSpawnAtPosition("MobIpc", map.GridCoords);
            patient = SSpawnAtPosition("MobHuman", map.GridCoords);
            var held = SSpawnAtPosition("Crowbar", map.GridCoords);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(ipc, held), Is.True);

            var module = InstallModule(ipc, "FishIpcModuleDefib");
            var vision = SComp<FishIpcDefibModuleComponent>(module);
            var action = vision.ActionEntity!.Value;
            SEntMan.EventBus.RaiseLocalEvent(ipc, new FishIpcDefibActionEvent
            {
                Performer = ipc,
                Action = (action, SComp<ActionComponent>(action)),
            });
            Assert.That(SComp<FishIpcDefibModuleComponent>(module).PadsEntity, Is.Null);

            Server.System<SharedHandsSystem>().TryDrop(ipc, held);
            SEntMan.EventBus.RaiseLocalEvent(ipc, new FishIpcDefibActionEvent
            {
                Performer = ipc,
                Action = (action, SComp<ActionComponent>(action)),
            });
            var pads = SComp<FishIpcDefibModuleComponent>(module).PadsEntity;
            Assert.That(pads, Is.Not.Null);
            SComp<FishIpcDefibModuleComponent>(module).DoAfter = TimeSpan.FromSeconds(0.2f);

            SEntMan.EventBus.RaiseLocalEvent(pads!.Value, new AfterInteractEvent(
                ipc, pads.Value, patient, SComp<TransformComponent>(patient).Coordinates, true));
        });

        await RunSeconds(0.6f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Alive));
            Assert.That(SEntMan.HasComponent<ElectrocutedComponent>(patient), Is.False);
        });
    }

    [Test]
    public async Task Defib_DoesNotHealLiving_AndBlocksCooldownAndRepeat()
    {
        var map = await Pair.CreateTestMap();
        EntityUid ipc = default;
        EntityUid patient = default;
        EntityUid module = default;

        await Server.WaitPost(() =>
        {
            DisableDamageVariance();
            ipc = SSpawnAtPosition("MobIpc", map.GridCoords);
            patient = SSpawnAtPosition("MobHuman", map.GridCoords);
            module = InstallModule(ipc, "FishIpcModuleDefib");
            SComp<FishIpcDefibModuleComponent>(module).DoAfter = TimeSpan.FromSeconds(0.4f);
            var action = SComp<FishIpcDefibModuleComponent>(module).ActionEntity!.Value;
            SEntMan.EventBus.RaiseLocalEvent(ipc, new FishIpcDefibActionEvent
            {
                Performer = ipc,
                Action = (action, SComp<ActionComponent>(action)),
            });
            Hurt(patient, "Asphyxiation", 20);
        });

        await Server.WaitPost(() =>
        {
            var pads = SComp<FishIpcDefibModuleComponent>(module).PadsEntity!.Value;
            var coords = SComp<TransformComponent>(patient).Coordinates;
            SEntMan.EventBus.RaiseLocalEvent(pads, new AfterInteractEvent(ipc, pads, patient, coords, true));
            Assert.That(SComp<DoAfterComponent>(ipc).DoAfters.Values.Count(d => !d.Cancelled && !d.Completed), Is.EqualTo(1));
            SEntMan.EventBus.RaiseLocalEvent(pads, new AfterInteractEvent(ipc, pads, patient, coords, true));
            Assert.That(SComp<DoAfterComponent>(ipc).DoAfters.Values.Count(d => !d.Cancelled && !d.Completed), Is.EqualTo(1));
        });

        await RunSeconds(0.7f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Damage(patient, "Asphyxiation"), Is.EqualTo(20f));
            Assert.That(SComp<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Alive));
            var action = SComp<FishIpcDefibModuleComponent>(module).ActionEntity!.Value;
            Assert.That(Server.System<SharedActionsSystem>().IsCooldownActive(SComp<ActionComponent>(action)), Is.True);

            var pads = SComp<FishIpcDefibModuleComponent>(module).PadsEntity!.Value;
            var before = SComp<DoAfterComponent>(ipc).DoAfters.Count;
            var coords = SComp<TransformComponent>(patient).Coordinates;
            SEntMan.EventBus.RaiseLocalEvent(pads, new AfterInteractEvent(ipc, pads, patient, coords, true));
            Assert.That(SComp<DoAfterComponent>(ipc).DoAfters.Count, Is.EqualTo(before));
        });
    }

    [Test]
    public async Task Defib_ZapAbortsWithoutPadsOrPower()
    {
        var map = await Pair.CreateTestMap();
        EntityUid ipc = default;
        EntityUid patient = default;
        EntityUid module = default;

        await Server.WaitPost(() =>
        {
            DisableDamageVariance();
            ipc = SSpawnAtPosition("MobIpc", map.GridCoords);
            patient = SSpawnAtPosition("MobHuman", map.GridCoords);
            module = InstallModule(ipc, "FishIpcModuleDefib");
            var action = SComp<FishIpcDefibModuleComponent>(module).ActionEntity!.Value;
            SEntMan.EventBus.RaiseLocalEvent(ipc, new FishIpcDefibActionEvent
            {
                Performer = ipc,
                Action = (action, SComp<ActionComponent>(action)),
            });
            Hurt(patient, "Asphyxiation", 40);
            Server.System<MobStateSystem>().ChangeMobState(patient, MobState.Dead);
            SComp<FishIpcDefibModuleComponent>(module).PadsActive = false;
            Assert.That(Server.System<SharedDoAfterSystem>().TryStartDoAfter(new DoAfterArgs(
                SEntMan, ipc, 0.05f, new FishIpcDefibDoAfterEvent(), module, patient)
            {
                NeedHand = false,
                BreakOnMove = false,
                BreakOnDamage = false,
            }), Is.True);
        });

        await RunSeconds(0.3f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Dead));
            Assert.That(Damage(patient, "Asphyxiation"), Is.EqualTo(40f));
        });
    }

    [Test]
    public async Task MesonModule_DoesNotStripWornEyeProtection()
    {
        await Server.WaitAssertion(() =>
        {
            var ipc = SSpawn("MobIpc");
            var protection = SEntMan.AddComponent<EyeProtectionComponent>(ipc);
            var module = InstallModule(ipc, "FishIpcModuleMeson");
            Assert.That(SComp<FishIpcVisionModuleComponent>(module).OwnsEyeProtection, Is.False);

            var action = SComp<FishIpcVisionModuleComponent>(module).ToggleActionEntity!.Value;
            SEntMan.EventBus.RaiseLocalEvent(ipc, new FishIpcModuleToggleActionEvent
            {
                Performer = ipc,
                Action = (action, SComp<ActionComponent>(action)),
            });

            Assert.That(SEntMan.HasComponent<EyeProtectionComponent>(ipc), Is.True);
            Assert.That(SComp<EyeProtectionComponent>(ipc), Is.EqualTo(protection));
        });
    }

    [Test]
    public async Task DuplicateModule_IsRejected()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var ipc = SSpawnAtPosition("MobIpc", map.GridCoords);
            Server.System<LockSystem>().Unlock(ipc, ipc);
            Assert.That(Server.System<SharedWiresSystem>().TogglePanel(ipc, SComp<WiresPanelComponent>(ipc), true), Is.True);

            var first = SSpawnAtPosition("FishIpcModuleWireInterface", map.GridCoords);
            var second = SSpawnAtPosition("FishIpcModuleWireInterface", map.GridCoords);
            var host = SComp<FishIpcModuleHostComponent>(ipc);
            var modules = Server.System<SharedFishIpcModuleSystem>();
            Assert.That(modules.CanInsertModule((ipc, host), (first, SComp<FishIpcModuleComponent>(first)), ipc), Is.True);
            Assert.That(Server.System<SharedContainerSystem>().Insert(first, host.ModuleContainer), Is.True);

            Assert.That(modules.CanInsertModule((ipc, host), (second, SComp<FishIpcModuleComponent>(second)), ipc), Is.False);
            Assert.That(Server.System<SharedContainerSystem>().Insert(second, host.ModuleContainer), Is.False);
            Assert.That(SComp<FishIpcModuleComponent>(second).Installed, Is.False);
            Assert.That(host.ModuleCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ModuleInsert_AndBatterySwap_AreImmediate()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var ipc = SSpawnAtPosition("MobIpc", map.GridCoords);
            Server.System<LockSystem>().Unlock(ipc, ipc);
            Assert.That(Server.System<SharedWiresSystem>().TogglePanel(ipc, SComp<WiresPanelComponent>(ipc), true), Is.True);

            var module = SSpawnAtPosition("FishIpcModuleEmpScreen", map.GridCoords);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(ipc, module), Is.True);
            var coords = SComp<TransformComponent>(ipc).Coordinates;
            SEntMan.EventBus.RaiseLocalEvent(ipc, new AfterInteractUsingEvent(ipc, module, ipc, coords, true));
            Assert.That(SComp<FishIpcModuleComponent>(module).Installed, Is.True);

            var cell = SSpawnAtPosition("PowerCellSmall", map.GridCoords);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(ipc, cell), Is.True);
            SEntMan.EventBus.RaiseLocalEvent(ipc, new InteractUsingEvent(ipc, cell, ipc, coords));
            Assert.That(Cell(ipc).Owner, Is.EqualTo(cell));
        });
    }
}
