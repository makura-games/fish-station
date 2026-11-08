#nullable enable
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Lock;
using Content.Shared.Radio.Components;
using Content.Shared.Wires;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.Tests._Fish.Ipc;

/// <summary>
/// Реальный InteractUsing: разблок + открытая панель, ключ внутрь, отвёрткой наружу.
/// </summary>
public sealed class FishIpcEncryptionKeyInteractionTest : InteractionTest
{
    [Test]
    public async Task ScrewdriverExtractsKeysFromOpenUnlockedChassis()
    {
        await SpawnTarget("MobIpc");
        var holder = Comp<EncryptionKeyHolderComponent>();
        var panel = Comp<WiresPanelComponent>();

        Assert.That(holder.KeyContainer.ContainedEntities, Has.Count.EqualTo(0));
        Assert.That(panel.Open, Is.False);

        // Замок + панель (OpeningTool у КПБ — Prying / лом).
        await Server.WaitPost(() =>
        {
            Server.System<LockSystem>().Unlock(STarget!.Value, SPlayer);
            Assert.That(Server.System<SharedWiresSystem>().TogglePanel(STarget.Value, panel, true), Is.True);
        });
        await RunTicks(1);
        Assert.That(panel.Open, Is.True);

        await InteractUsing("EncryptionKeyEngineering");
        Assert.That(holder.KeyContainer.ContainedEntities, Has.Count.EqualTo(1));

        // Отвёртка должна сразу вытащить ключ из открытого шасси.
        await InteractUsing(Screw);
        Assert.Multiple(() =>
        {
            Assert.That(holder.KeyContainer.ContainedEntities, Has.Count.EqualTo(0));
            Assert.That(holder.Channels, Has.Count.EqualTo(0));
        });

        await AssertEntityLookup(("EncryptionKeyEngineering", 1));
    }

    [Test]
    public async Task ClosedPanelBlocksScrewdriverExtraction()
    {
        await SpawnTarget("MobIpc");
        var holder = Comp<EncryptionKeyHolderComponent>();

        await Server.WaitPost(() =>
        {
            Server.System<LockSystem>().Unlock(STarget!.Value, SPlayer);
            var key = SEntMan.Spawn("EncryptionKeyEngineering");
            Assert.That(SEntMan.System<SharedContainerSystem>().Insert(key, holder.KeyContainer), Is.True);
        });
        await RunTicks(1);
        Assert.That(holder.KeyContainer.ContainedEntities, Has.Count.EqualTo(1));

        await InteractUsing(Screw);
        Assert.That(holder.KeyContainer.ContainedEntities, Has.Count.EqualTo(1));
    }
}
