using System.Diagnostics.CodeAnalysis;
using Content.Shared.Clothing.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.Shared._Fish.Clothing;

/// <summary>
///     Позволяет прикреплять медали к форме и отображать последнюю из них поверх спрайта комбинезона.
/// </summary>
public sealed partial class ClothingMedalsSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private const int MedalSlotCount = 4;
    private const string MedalState = "equipped-NECK";

    /// <summary>
    ///     Порядок прикрепления медалей для каждой формы. На модели показываем последнюю,
    ///     как обычный предмет в слоте шеи.
    /// </summary>
    private readonly Dictionary<EntityUid, List<EntityUid>> _attachmentOrder = new();

    private static readonly VerbCategory MedalCategory =
        new("clothing-medal-category", "/Textures/Interface/VerbIcons/group.svg.192dpi.png");

    private static readonly string[] MedalSlotIds =
    {
        "medal1",
        "medal2",
        "medal3",
        "medal4",
    };

    private static readonly SpriteSpecifier AttachIcon =
        new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/insert.svg.192dpi.png"));

    private static readonly SpriteSpecifier DetachIcon =
        new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/eject.svg.192dpi.png"));

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClothingComponent, EntInsertedIntoContainerMessage>(OnMedalInserted);
        SubscribeLocalEvent<ClothingComponent, EntRemovedFromContainerMessage>(OnMedalRemoved);
        SubscribeLocalEvent<ClothingComponent, ComponentShutdown>(OnClothingShutdown);
        SubscribeLocalEvent<ClothingComponent, InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>>>(OnGetEquipmentVerbs);
        SubscribeLocalEvent<ClothingComponent, ExaminedEvent>(OnExamined);
    }

    private void OnClothingShutdown(Entity<ClothingComponent> ent, ref ComponentShutdown args)
    {
        _attachmentOrder.Remove(ent);
    }

    private void OnExamined(Entity<ClothingComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !HasComp<ItemSlotsComponent>(ent))
            return;

        var medals = new List<string>(MedalSlotCount);
        foreach (var slotId in MedalSlotIds)
        {
            if (_itemSlots.TryGetSlot(ent.Owner, slotId, out var slot) &&
                slot.Item is { } medal)
                medals.Add(Name(medal));
        }

        if (medals.Count == 0)
            return;

        args.PushMarkup(Loc.GetString("clothing-medal-examine",
            ("medals", string.Join(", ", medals))));
    }

    private void OnMedalInserted(Entity<ClothingComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (!IsMedalSlot(args.Container.ID))
            return;

        if (!_attachmentOrder.TryGetValue(ent, out var order))
        {
            order = new List<EntityUid>(MedalSlotCount);
            _attachmentOrder[ent] = order;
        }

        // Перемещаем медаль в конец списка, чтобы она считалась последней прикреплённой.
        order.Remove(args.Entity);
        order.Add(args.Entity);

        _item.VisualsChanged(ent);
    }

    private void OnMedalRemoved(Entity<ClothingComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (!IsMedalSlot(args.Container.ID))
            return;

        if (_attachmentOrder.TryGetValue(ent, out var order))
        {
            order.Remove(args.Entity);
            if (order.Count == 0)
                _attachmentOrder.Remove(ent);
        }

        _item.VisualsChanged(ent);
    }

    private void OnGetEquipmentVerbs(Entity<ClothingComponent> ent,
        ref InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>> args)
    {
        var verbArgs = args.Args;
        if (!verbArgs.CanAccess || !verbArgs.CanInteract)
            return;

        if (!HasComp<ItemSlotsComponent>(ent))
            return;

        if (verbArgs.Using is { } used && CanAttachMedal(ent, used, verbArgs.User, out _))
        {
            verbArgs.Verbs.Add(new EquipmentVerb
            {
                Text = Loc.GetString("clothing-medal-attach-verb"),
                Icon = AttachIcon,
                Category = MedalCategory,
                Act = () => TryAttachMedal(ent, used, verbArgs.User),
            });
        }

        foreach (var slotId in MedalSlotIds)
        {
            if (!_itemSlots.TryGetSlot(ent.Owner, slotId, out var slot) ||
                slot.Item is not { } medal ||
                !CanDetachMedal(ent, slot, verbArgs.User))
                continue;

            var capturedSlot = slot;
            verbArgs.Verbs.Add(new EquipmentVerb
            {
                Text = Loc.GetString("clothing-medal-detach-verb-named", ("medal", medal)),
                Icon = DetachIcon,
                Category = MedalCategory,
                Act = () => TryDetachMedal(ent, capturedSlot, verbArgs.User),
            });
        }
    }

    /// <summary>
    ///     Добавляет слой последней прикреплённой медали в список слоёв одежды.
    /// </summary>
    /// <remarks>
    ///     Вызывается из <see cref="Content.Client.Clothing.ClientClothingSystem"/> после слоёв формы,
    ///     чтобы медали отрисовывались поверх комбинезона.
    /// </remarks>
    public void AppendMedalVisuals(EntityUid uniform, List<(string, PrototypeLayerData)> layers)
    {
        if (!TryGetDisplayedMedal(uniform, out var medal))
            return;

        if (!TryComp<ClothingComponent>(medal, out var medalClothing) ||
            medalClothing.RsiPath == null)
            return;

        // Показываем только последнюю прикреплённую медаль, без смещения —
        // так же, как обычный предмет в слоте шеи.
        layers.Add(("medal-attachment", new PrototypeLayerData
        {
            RsiPath = medalClothing.RsiPath,
            State = MedalState,
        }));
    }

    private bool TryGetDisplayedMedal(EntityUid uniform, out EntityUid medal)
    {
        medal = default;
        if (!HasComp<ItemSlotsComponent>(uniform))
            return false;

        if (_attachmentOrder.TryGetValue(uniform, out var order))
        {
            for (var i = order.Count - 1; i >= 0; i--)
            {
                var candidate = order[i];
                if (!IsAttachedMedal(uniform, candidate))
                {
                    order.RemoveAt(i);
                    continue;
                }

                medal = candidate;
                return true;
            }

            _attachmentOrder.Remove(uniform);
        }

        // После переподключения порядок вставки неизвестен — берём последнюю заполненную ячейку.
        for (var i = MedalSlotCount - 1; i >= 0; i--)
        {
            if (_itemSlots.TryGetSlot(uniform, MedalSlotIds[i], out var slot) &&
                slot.Item is { } item)
            {
                medal = item;
                return true;
            }
        }

        return false;
    }

    private bool IsAttachedMedal(EntityUid uniform, EntityUid medal)
    {
        foreach (var slotId in MedalSlotIds)
        {
            if (_itemSlots.TryGetSlot(uniform, slotId, out var slot) && slot.Item == medal)
                return true;
        }

        return false;
    }

    private bool TryAttachMedal(EntityUid uniform, EntityUid medal, EntityUid user)
    {
        if (!CanAttachMedal(uniform, medal, user, out var slot) ||
            slot.ContainerSlot == null)
            return false;

        if (!_hands.TryDropIntoContainer(user, medal, slot.ContainerSlot))
            return false;

        _popup.PopupEntity(Loc.GetString("clothing-medal-attached"), uniform);
        return true;
    }

    private bool CanAttachMedal(EntityUid uniform, EntityUid medal, EntityUid user,
        [NotNullWhen(true)] out ItemSlot? slot)
    {
        slot = null;

        foreach (var slotId in MedalSlotIds)
        {
            if (!_itemSlots.TryGetSlot(uniform, slotId, out var candidate) || candidate.HasItem)
                continue;

            if (!_itemSlots.CanInsert(uniform, candidate, medal, user))
                continue;

            slot = candidate;
            return true;
        }

        return false;
    }

    private bool TryDetachMedal(EntityUid uniform, ItemSlot slot, EntityUid user)
    {
        if (!CanDetachMedal(uniform, slot, user))
            return false;

        return _itemSlots.TryEjectToHands(uniform, slot, user);
    }

    private bool CanDetachMedal(EntityUid uniform, ItemSlot slot, EntityUid user)
    {
        return slot.HasItem && _itemSlots.CanEject(uniform, slot, user);
    }

    private static bool IsMedalSlot(string slotId)
    {
        for (var i = 0; i < MedalSlotCount; i++)
        {
            if (MedalSlotIds[i] == slotId)
                return true;
        }

        return false;
    }
}
