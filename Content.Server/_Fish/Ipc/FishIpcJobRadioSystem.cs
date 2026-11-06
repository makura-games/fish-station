using System.Linq;
using Content.Shared._Fish.Ipc;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Robust.Shared.Containers;

namespace Content.Server._Fish.Ipc;

/// <summary>
/// КПБ не носит гарнитуру при спавне: ключи из job-гарнитуры переносятся в корпус.
/// </summary>
public sealed partial class FishIpcJobRadioSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private FishIpcRadioSystem _radio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishIpcComponent, StartingGearEquippedEvent>(OnStartingGear);
    }

    private void OnStartingGear(Entity<FishIpcComponent> ent, ref StartingGearEquippedEvent args)
    {
        if (!_inventory.TryGetSlotEntity(ent.Owner, "ears", out var ears) ||
            !HasComp<HeadsetComponent>(ears.Value))
            return;

        if (TryComp<EncryptionKeyHolderComponent>(ears.Value, out var headsetKeys) &&
            TryComp<EncryptionKeyHolderComponent>(ent.Owner, out var bodyKeys))
        {
            foreach (var key in headsetKeys.KeyContainer.ContainedEntities.ToArray())
            {
                // Сначала в корпус; если слоты кончились — в руки/на пол, не удалять с гарнитурой.
                if (bodyKeys.KeyContainer.ContainedEntities.Count < bodyKeys.KeySlots &&
                    _container.Insert(key, bodyKeys.KeyContainer))
                    continue;

                _container.Remove(key, headsetKeys.KeyContainer, force: true);
                _hands.PickupOrDrop(ent.Owner, key);
            }
        }

        _inventory.TryUnequip(ent.Owner, "ears", silent: true, force: true);
        if (!TerminatingOrDeleted(ears.Value))
            QueueDel(ears.Value);

        _radio.SetRadioEnabled(ent, !ent.Comp.PowerDown);
    }
}
