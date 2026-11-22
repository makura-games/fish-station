#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Server._Fish.Ipc.Modules;
using Content.Shared._Fish.Ipc.Modules;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Fish.Ipc;

/// <summary>
/// Базовые инварианты слотов модулей КПБ: insert, exclusion group, cap, host markers.
/// </summary>
[TestFixture]
[TestOf(typeof(FishIpcModuleSystem))]
public sealed class FishIpcModuleSystemTest : GameTest
{
    [Test]
    public async Task InsertModuleGrantsHostMarkerAndCountsTowardCap()
    {
        EntityUid ipc = default;
        EntityUid module = default;
        FishIpcModuleHostComponent host = default!;
        SharedContainerSystem containers = default!;
        var inserted = false;

        await Server.WaitPost(() =>
        {
            ipc = SSpawn("MobIpc");
            module = SSpawn("FishIpcModuleEmpScreen");
            host = SComp<FishIpcModuleHostComponent>(ipc);
            containers = Server.System<SharedContainerSystem>();

            var moduleSys = Server.System<FishIpcModuleSystem>();
            var hostEnt = new Entity<FishIpcModuleHostComponent>(ipc, host);
            var modEnt = new Entity<FishIpcModuleComponent>(module, SComp<FishIpcModuleComponent>(module));

            Assert.That(moduleSys.CanInsertModule(hostEnt, modEnt), Is.True);
            // Прямой Insert в контейнер — панель нужна только AfterInteractUsing.
            inserted = containers.Insert(module, host.ModuleContainer);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(inserted, Is.True);
                Assert.That(host.ModuleCount, Is.EqualTo(1));
                Assert.That(SEntMan.HasComponent<FishIpcEmpScreenHostComponent>(ipc), Is.True);
                Assert.That(SComp<FishIpcModuleComponent>(module).Installed, Is.True);
                Assert.That(SComp<FishIpcModuleComponent>(module).InstalledEntity, Is.EqualTo(ipc));
            });
        });
    }

    [Test]
    public async Task ExclusionGroupBlocksSecondVisionModule()
    {
        EntityUid ipc = default;
        FishIpcModuleHostComponent host = default!;
        FishIpcModuleSystem moduleSys = default!;
        var canInsertSecond = true;

        await Server.WaitPost(() =>
        {
            ipc = SSpawn("MobIpc");
            host = SComp<FishIpcModuleHostComponent>(ipc);
            moduleSys = Server.System<FishIpcModuleSystem>();
            var containers = Server.System<SharedContainerSystem>();

            var first = SSpawn("FishIpcModuleMedHud");
            Assert.That(containers.Insert(first, host.ModuleContainer), Is.True);

            var second = SSpawn("FishIpcModuleSecHud");
            var hostEnt = new Entity<FishIpcModuleHostComponent>(ipc, host);
            var modEnt = new Entity<FishIpcModuleComponent>(second, SComp<FishIpcModuleComponent>(second));
            canInsertSecond = moduleSys.CanInsertModule(hostEnt, modEnt);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(canInsertSecond, Is.False, "vision exclusion должен блокировать второй HUD");
            Assert.That(host.ModuleCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task MaxModulesCapIsEnforced()
    {
        EntityUid ipc = default;
        FishIpcModuleHostComponent host = default!;
        FishIpcModuleSystem moduleSys = default!;
        var canInsertFourth = false;
        var canInsertFifth = true;

        await Server.WaitPost(() =>
        {
            ipc = SSpawn("MobIpc");
            host = SComp<FishIpcModuleHostComponent>(ipc);
            moduleSys = Server.System<FishIpcModuleSystem>();
            var containers = Server.System<SharedContainerSystem>();
            var hostEnt = new Entity<FishIpcModuleHostComponent>(ipc, host);

            Assert.That(containers.Insert(SSpawn("FishIpcModuleWireInterface"), host.ModuleContainer), Is.True);
            Assert.That(containers.Insert(SSpawn("FishIpcModuleApcAdapter"), host.ModuleContainer), Is.True);
            Assert.That(containers.Insert(SSpawn("FishIpcModuleVocalMod"), host.ModuleContainer), Is.True);

            var fourth = SSpawn("FishIpcModuleToolHand");
            canInsertFourth = moduleSys.CanInsertModule(hostEnt, new Entity<FishIpcModuleComponent>(fourth, SComp<FishIpcModuleComponent>(fourth)));
            Assert.That(containers.Insert(fourth, host.ModuleContainer), Is.True);

            var fifth = SSpawn("FishIpcModuleNanoRepair");
            canInsertFifth = moduleSys.CanInsertModule(hostEnt, new Entity<FishIpcModuleComponent>(fifth, SComp<FishIpcModuleComponent>(fifth)));
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(host.ModuleCount, Is.EqualTo(4));
            Assert.That(canInsertFourth, Is.True);
            Assert.That(canInsertFifth, Is.False);
        });
    }
}
