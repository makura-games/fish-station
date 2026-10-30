using Content.Shared.Clothing.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Item;
using Robust.Shared.Containers;

namespace Content.Shared._Fish.Clothing;

/// <summary>
/// Обновляет визуалы носителя при вставке/извлечении медали из формы.
/// </summary>
public sealed partial class MedalHolderSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedItemSystem _item = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedalHolderComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<MedalHolderComponent, EntRemovedFromContainerMessage>(OnRemoved);
    }

    private void OnInserted(Entity<MedalHolderComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SlotId)
            return;

        RefreshWearerVisuals(ent);
    }

    private void OnRemoved(Entity<MedalHolderComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SlotId)
            return;

        RefreshWearerVisuals(ent);
    }

    private void RefreshWearerVisuals(EntityUid clothing)
    {
        // VisualsChanged на форме поднимает событие на носителе, если форма надета.
        if (!TryComp<ClothingComponent>(clothing, out var clothingComp) || clothingComp.InSlot == null)
            return;

        _item.VisualsChanged(clothing);
    }

    /// <summary>
    /// Возвращает медаль из слота формы, если она вставлена.
    /// </summary>
    public bool TryGetMedal(Entity<MedalHolderComponent?> ent, out EntityUid medal)
    {
        medal = default;
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!_itemSlots.TryGetSlot(ent.Owner, ent.Comp.SlotId, out var slot) || slot.Item is not { } item)
            return false;

        medal = item;
        return true;
    }
}
