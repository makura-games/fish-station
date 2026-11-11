#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Fish.Ipc;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Lock;
using Content.Shared.PowerCell.Components;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Wires;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.Ipc;

/// <summary>
/// Вставка батареи и ключей шифрования в КПБ при снятом замке и открытой панели
/// (тот же гейт, что у модулей; самообслуживание разрешено).
/// </summary>
[TestFixture]
[TestOf(typeof(SharedFishIpcSystem))]
public sealed class FishIpcBatteryKeyTest : GameTest
{
    private static readonly ProtoId<RadioChannelPrototype> EngineeringChannel = "Engineering";

    [Test]
    public async Task SelfSwapPowerCellWithOpenPanel()
    {
        EntityUid ipc = default;
        EntityUid newCell = default;
        EntityUid oldCell = default;
        var swapped = false;

        await Server.WaitPost(() =>
        {
            ipc = SSpawn("MobIpc");
            UnlockAndOpenPanel(ipc);

            var powerSlot = SComp<PowerCellSlotComponent>(ipc);
            var slots = Server.System<ItemSlotsSystem>();
            Assert.That(slots.TryGetSlot(ipc, powerSlot.CellSlotId, out var slot), Is.True);
            Assert.That(slot!.Item, Is.Not.Null);
            oldCell = slot.Item!.Value;

            newCell = SSpawn("PowerCellSmall");
            var coords = SEntMan.GetComponent<TransformComponent>(ipc).Coordinates;
            var ev = new InteractUsingEvent(ipc, newCell, ipc, coords);
            SEntMan.EventBus.RaiseLocalEvent(ipc, ev);

            swapped = ev.Handled;
        });

        await RunSeconds(1.2f);

        await Server.WaitAssertion(() =>
        {
            swapped = swapped && SComp<ItemSlotsComponent>(ipc).Slots["cell_slot"].Item == newCell && SEntMan.EntityExists(oldCell);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(swapped, Is.True);
                Assert.That(SComp<ItemSlotsComponent>(ipc).Slots["cell_slot"].Item, Is.EqualTo(newCell));
                Assert.That(SEntMan.EntityExists(oldCell), Is.True);
            });
        });
    }

    [Test]
    public async Task SelfInsertEncryptionKeyWithOpenPanel()
    {
        EntityUid ipc = default;
        EntityUid key = default;
        var inserted = false;

        await Server.WaitPost(() =>
        {
            ipc = SSpawn("MobIpc");
            UnlockAndOpenPanel(ipc);

            key = SSpawn("EncryptionKeyEngineering");
            var coords = SEntMan.GetComponent<TransformComponent>(ipc).Coordinates;
            var ev = new InteractUsingEvent(ipc, key, ipc, coords);
            SEntMan.EventBus.RaiseLocalEvent(ipc, ev);

            var holder = SComp<EncryptionKeyHolderComponent>(ipc);
            inserted = ev.Handled && holder.KeyContainer.ContainedEntities.Contains(key);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(inserted, Is.True);
            Assert.That(SComp<EncryptionKeyHolderComponent>(ipc).Channels, Does.Contain(EngineeringChannel));
        });
    }

    [Test]
    public async Task ClosedPanelBlocksCellAndKey()
    {
        EntityUid ipc = default;
        var cellBlocked = false;
        var keyBlocked = false;

        await Server.WaitPost(() =>
        {
            ipc = SSpawn("MobIpc");
            Server.System<LockSystem>().Unlock(ipc, ipc);

            var slots = Server.System<ItemSlotsSystem>();
            var powerSlot = SComp<PowerCellSlotComponent>(ipc);
            Assert.That(slots.TryGetSlot(ipc, powerSlot.CellSlotId, out var slot), Is.True);
            var oldCell = slot!.Item;

            var coords = SEntMan.GetComponent<TransformComponent>(ipc).Coordinates;

            var newCell = SSpawn("PowerCellSmall");
            var cellEv = new InteractUsingEvent(ipc, newCell, ipc, coords);
            SEntMan.EventBus.RaiseLocalEvent(ipc, cellEv);
            cellBlocked = cellEv.Handled && slot.Item == oldCell;

            var key = SSpawn("EncryptionKeyEngineering");
            var keyEv = new InteractUsingEvent(ipc, key, ipc, coords);
            SEntMan.EventBus.RaiseLocalEvent(ipc, keyEv);
            keyBlocked = keyEv.Handled
                         && !SComp<EncryptionKeyHolderComponent>(ipc).KeyContainer.ContainedEntities.Contains(key);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(cellBlocked, Is.True);
                Assert.That(keyBlocked, Is.True);
            });
        });
    }

    private void UnlockAndOpenPanel(EntityUid ipc)
    {
        Server.System<LockSystem>().Unlock(ipc, ipc);
        var panel = SComp<WiresPanelComponent>(ipc);
        Assert.That(Server.System<SharedWiresSystem>().TogglePanel(ipc, panel, true), Is.True);
        Assert.That(panel.Open, Is.True);
    }
}
