#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Fish.Ipc;
using Content.Shared._Fish.Ipc.Modules;
using Content.Shared.Alert;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.Lock;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Overlays;
using Content.Shared.PowerCell.Components;
using Content.Shared.Preferences;
using Content.Shared.Wires;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._Fish.Ipc;

/// <summary>
/// Пути, которым нужна живая сессия: перезапуск с ReturnToBody и стартовый модуль отдела.
/// </summary>
[TestFixture]
public sealed class FishIpcConnectedTest : FishIpcGameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true };

    [Test]
    public async Task Reboot_WithMindOutsideBody_Revives()
    {
        EntityUid ipc = default;
        EntityUid dummy = default;

        await Server.WaitPost(() =>
        {
            DisableDamageVariance();
            var player = Server.ResolveDependency<IPlayerManager>().Sessions.Single();
            var minds = Server.System<SharedMindSystem>();
            ipc = SSpawn("MobIpc");
            dummy = SSpawn("MobHuman");
            var mind = minds.CreateMind(player.UserId, "ipc-reboot");
            minds.TransferTo(mind.Owner, ipc, createGhost: false, mind: mind.Comp);
            minds.TransferTo(mind.Owner, dummy, createGhost: false, mind: mind.Comp);
            Assert.That(mind.Comp.CurrentEntity, Is.EqualTo(dummy));
            Assert.That(mind.Comp.UserId, Is.EqualTo(player.UserId));

            Hurt(ipc, "Blunt", 40);
            Server.System<MobStateSystem>().ChangeMobState(ipc, MobState.Dead);
            // Живой перезапускает мёртвое шасси. Разум при этом сидит в dummy, не в теле.
            Assert.That(Server.System<SharedDoAfterSystem>().TryStartDoAfter(new DoAfterArgs(
                SEntMan,
                dummy,
                0.05f,
                new FishDeadStartupDoAfterEvent(),
                eventTarget: ipc,
                target: ipc)
            {
                NeedHand = false,
                BreakOnMove = false,
                BreakOnDamage = false,
            }), Is.True);
        });

        await RunSeconds(0.3f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<MobStateComponent>(ipc).CurrentState, Is.EqualTo(MobState.Alive));
            var mindId = Server.System<SharedMindSystem>().GetMind(dummy);
            Assert.That(mindId, Is.Not.Null);
            Assert.That(SComp<MindComponent>(mindId!.Value).CurrentEntity, Is.EqualTo(dummy));
        });
    }

    [Test]
    public async Task JobSpawn_GrantsDepartmentVisionModule()
    {
        EntityUid security = default;
        EntityUid medical = default;

        await Server.WaitPost(() =>
        {
            var player = Server.ResolveDependency<IPlayerManager>().Sessions.Single();
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();
            security = SSpawn("MobIpc");
            medical = SSpawn("MobIpc");
            RaiseJob(security, player, "SecurityOfficer", profile);
            RaiseJob(medical, player, "MedicalDoctor", profile);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<ShowJobIconsComponent>(security), Is.True);
                Assert.That(SEntMan.HasComponent<ShowHealthBarsComponent>(medical), Is.True);
                Assert.That(SComp<FishIpcModuleHostComponent>(security).ModuleCount, Is.EqualTo(1));
                Assert.That(SComp<FishIpcModuleHostComponent>(medical).ModuleCount, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task CombatRole_GrantsEmpAndProfessionModules()
    {
        EntityUid ertSec = default;
        EntityUid ertMed = default;
        EntityUid nukeOp = default;
        EntityUid nukeMed = default;
        var ertEject = true;
        var nukeEject = true;
        EntityUid? ertCell = null;
        EntityUid? nukeCell = null;

        await Server.WaitPost(() =>
        {
            var modules = Server.System<Content.Server._Fish.Ipc.FishIpcJobModuleSystem>();
            ertSec = SSpawn("MobIpc");
            ertMed = SSpawn("MobIpc");
            nukeOp = SSpawn("MobIpc");
            nukeMed = SSpawn("MobIpc");
            modules.TryGrantForRole(ertSec, "ERTSecurity");
            modules.TryGrantForRole(ertMed, "ERTMedical");
            modules.TryGrantForRole(nukeOp, "NukeopsOperative");
            modules.TryGrantForRole(nukeMed, "NukeopsMedic");

            // Печать проверяем при открытой панели: иначе TryEject=false из-за замка/панели.
            OpenChassisPanel(ertSec);
            OpenChassisPanel(nukeOp);
            ertCell = GetCellUid(ertSec);
            nukeCell = GetCellUid(nukeOp);
            var slots = Server.System<ItemSlotsSystem>();
            ertEject = slots.TryEject(ertSec, SComp<PowerCellSlotComponent>(ertSec).CellSlotId, null, out _);
            nukeEject = slots.TryEject(nukeOp, SComp<PowerCellSlotComponent>(nukeOp).CellSlotId, null, out _);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                AssertHasModules(ertSec, "FishIpcModuleEmpScreen", "FishIpcModuleNightVision", "FishIpcModuleNanoRepair");
                AssertHasModules(ertMed, "FishIpcModuleEmpScreen", "FishIpcModuleMedHud", "FishIpcModuleDefib");
                AssertHasModules(nukeOp, "FishIpcModuleEmpScreen", "FishIpcModuleNightVision", "FishIpcModuleNanoRepair");
                AssertHasModules(nukeMed, "FishIpcModuleEmpScreen", "FishIpcModuleMedHud", "FishIpcModuleDefib");
                AssertSealedSyndicateCell(ertSec, ertCell, ertEject);
                AssertSealedSyndicateCell(nukeOp, nukeCell, nukeEject);
            });
        });
    }

    private void OpenChassisPanel(EntityUid ipc)
    {
        Server.System<LockSystem>().Unlock(ipc, ipc);
        var panel = SComp<WiresPanelComponent>(ipc);
        Assert.That(Server.System<SharedWiresSystem>().TogglePanel(ipc, panel, true), Is.True);
    }

    private EntityUid? GetCellUid(EntityUid ipc)
    {
        var slots = Server.System<ItemSlotsSystem>();
        Assert.That(slots.TryGetSlot(ipc, SComp<PowerCellSlotComponent>(ipc).CellSlotId, out var slot), Is.True);
        return slot!.Item;
    }

    private void AssertSealedSyndicateCell(EntityUid ipc, EntityUid? expectedCell, bool ejectSucceeded)
    {
        Assert.That(SEntMan.HasComponent<FishIpcSealedBatteryComponent>(ipc), Is.True);
        Assert.That(ejectSucceeded, Is.False);
        var slots = Server.System<ItemSlotsSystem>();
        var cellSlot = SComp<PowerCellSlotComponent>(ipc);
        Assert.That(slots.TryGetSlot(ipc, cellSlot.CellSlotId, out var slot), Is.True);
        Assert.That(slot!.Item, Is.EqualTo(expectedCell));
        Assert.That(expectedCell, Is.Not.Null);
        Assert.That(SEntMan.GetComponent<MetaDataComponent>(expectedCell!.Value).EntityPrototype?.ID,
            Is.EqualTo("PowerCellSyndicate"));
    }

    private void AssertHasModules(EntityUid ipc, params string[] expected)
    {
        var host = SComp<FishIpcModuleHostComponent>(ipc);
        Assert.That(host.ModuleCount, Is.EqualTo(expected.Length), ipc.ToString());
        var ids = host.ModuleContainer.ContainedEntities
            .Select(uid => SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID)
            .ToHashSet();
        foreach (var id in expected)
            Assert.That(ids, Does.Contain(id), $"{ipc}: missing {id}");
    }

    [Test]
    public async Task BatteryEjectAndInsert_ClientDoesNotCrash()
    {
        var catcher = new LogCatcher();
        var logMan = Client.ResolveDependency<ILogManager>();
        await Client.WaitPost(() => logMan.RootSawmill.AddHandler(catcher));

        try
        {
            EntityUid ipc = default;
            EntityUid cell = default;

            await Server.WaitPost(() =>
            {
                var player = Server.ResolveDependency<IPlayerManager>().Sessions.Single();
                var minds = Server.System<SharedMindSystem>();
                ipc = SSpawn("MobIpc");
                var mind = minds.CreateMind(player.UserId, "ipc-battery");
                minds.TransferTo(mind.Owner, ipc, createGhost: false, mind: mind.Comp);
                Server.System<LockSystem>().Unlock(ipc, ipc);
                var panel = SComp<WiresPanelComponent>(ipc);
                Assert.That(Server.System<SharedWiresSystem>().TogglePanel(ipc, panel, true), Is.True);
            });

            await RunSeconds(1);

            await Server.WaitPost(() =>
            {
                var slots = Server.System<ItemSlotsSystem>();
                var cellSlot = SComp<PowerCellSlotComponent>(ipc);
                Assert.That(slots.TryEject(ipc, cellSlot.CellSlotId, null, out var ejected), Is.True);
                cell = ejected!.Value;
            });

            await RunSeconds(1);
            await AssertClientBatteryIcons(ipc, catcher);

            await Server.WaitPost(() =>
            {
                var slots = Server.System<ItemSlotsSystem>();
                var cellSlot = SComp<PowerCellSlotComponent>(ipc);
                Assert.That(slots.TryInsert(ipc, cellSlot.CellSlotId, cell, null), Is.True);
            });

            await RunSeconds(1);
            await AssertClientBatteryIcons(ipc, catcher);
        }
        finally
        {
            await Client.WaitPost(() => logMan.RootSawmill.RemoveHandler(catcher));
        }
    }

    /// <summary>
    /// Тот же путь, что у HUD: прототип берётся из ключа алерта, и GetIcon не должен падать на null severity.
    /// </summary>
    private async Task AssertClientBatteryIcons(EntityUid ipc, LogCatcher catcher)
    {
        await Client.WaitAssertion(() =>
        {
            var uid = ToClientUid(ipc);
            var local = Client.ResolveDependency<ISharedPlayerManager>().LocalEntity;
            Assert.That(local, Is.EqualTo(uid));
            Assert.That(CEntMan.TryGetComponent(uid, out AlertsComponent? alerts), Is.True);

            var protos = Client.ResolveDependency<IPrototypeManager>();
            foreach (var (key, state) in alerts!.Alerts)
            {
                if (key.AlertType is not { } alertType)
                    continue;

                var alert = protos.Index<AlertPrototype>(alertType);
                if (alert.SupportsSeverity)
                    Assert.That(state.Severity, Is.Not.Null, $"alert {alertType}");

                Assert.That(alert.GetIcon(state.Severity), Is.Not.Null);
            }

            var crash = catcher.CaughtLogs
                .Select(log => log.RenderMessage())
                .Where(message => message.Contains("No severity specified") || message.Contains("ResetPredictedEntities"))
                .ToArray();
            Assert.That(crash, Is.Empty);
        });
    }

    private void RaiseJob(EntityUid ipc, ICommonSession player, string jobId, HumanoidCharacterProfile profile)
    {
        var ev = new PlayerSpawnCompleteEvent(ipc, player, jobId, true, true, 1, EntityUid.Invalid, profile, true);
        SEntMan.EventBus.RaiseLocalEvent(ipc, ev);
    }
}
