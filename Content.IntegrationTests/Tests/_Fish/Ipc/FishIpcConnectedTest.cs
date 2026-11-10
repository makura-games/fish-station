#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Fish.Ipc;
using Content.Shared._Fish.Ipc.Modules;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Overlays;
using Content.Shared.Preferences;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

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

    private void RaiseJob(EntityUid ipc, ICommonSession player, string jobId, HumanoidCharacterProfile profile)
    {
        var ev = new PlayerSpawnCompleteEvent(ipc, player, jobId, true, true, 1, EntityUid.Invalid, profile, true);
        SEntMan.EventBus.RaiseLocalEvent(ipc, ev);
    }
}
