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
using Robust.Shared.GameStates;
using Robust.Shared.Network;
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
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private INetManager _net = default!;

    private const int MedalSlotCount = 4;
    private const string MedalState = "equipped-NECK";
    private const string UniformSlot = "jumpsuit";

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
        SubscribeLocalEvent<ClothingMedalsComponent, AfterAutoHandleStateEvent>(OnOrderStateHandled);
        SubscribeLocalEvent<ClothingComponent, InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>>>(OnGetEquipmentVerbs);
        SubscribeLocalEvent<ClothingComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<InventoryComponent, ExaminedEvent>(OnWearerExamined);
    }

    private void OnOrderStateHandled(Entity<ClothingMedalsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _item.VisualsChanged(ent);
    }

    private void OnExamined(Entity<ClothingComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var medals = GetMedalNames(ent);
        if (medals.Count == 0)
            return;

        args.PushMarkup(Loc.GetString("clothing-medal-examine",
            ("medals", string.Join(", ", medals))));
    }

    /// <summary>
    ///     При осмотре персонажа (Shift) показывает медали, прикреплённые к его форме.
    /// </summary>
    private void OnWearerExamined(Entity<InventoryComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange ||
            !_inventory.TryGetSlotEntity(ent, UniformSlot, out var uniform))
            return;

        var medals = GetMedalNames(uniform.Value);
        if (medals.Count == 0)
            return;

        args.PushMarkup(Loc.GetString("clothing-medal-examine-wearer",
            ("medals", string.Join(", ", medals))));
    }

    private List<string> GetMedalNames(EntityUid uniform)
    {
        var medals = new List<string>(MedalSlotCount);
        if (!HasComp<ItemSlotsComponent>(uniform))
            return medals;

        foreach (var slotId in MedalSlotIds)
        {
            if (_itemSlots.TryGetSlot(uniform, slotId, out var slot) &&
                slot.Item is { } medal)
                medals.Add(Name(medal));
        }

        return medals;
    }

    private void OnMedalInserted(Entity<ClothingComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (!IsMedalSlot(args.Container.ID))
            return;

        // Порядок ведёт только сервер, клиенты получают его вместе с состоянием компонента.
        if (_net.IsServer)
        {
            var medals = EnsureComp<ClothingMedalsComponent>(ent);

            // Перемещаем медаль в конец списка, чтобы она считалась последней прикреплённой.
            medals.Order.Remove(args.Entity);
            medals.Order.Add(args.Entity);
            Dirty(ent, medals);
        }

        _item.VisualsChanged(ent);
    }

    private void OnMedalRemoved(Entity<ClothingComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (!IsMedalSlot(args.Container.ID))
            return;

        if (_net.IsServer && TryComp<ClothingMedalsComponent>(ent, out var medals))
        {
            medals.Order.Remove(args.Entity);
            Dirty(ent, medals);
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

        if (TryComp<ClothingMedalsComponent>(uniform, out var medals))
        {
            for (var i = medals.Order.Count - 1; i >= 0; i--)
            {
                var candidate = medals.Order[i];
                if (!IsAttachedMedal(uniform, candidate))
                    continue;

                medal = candidate;
                return true;
            }
        }

        // Порядок ещё не пришёл или не знает о медалях — берём последнюю заполненную ячейку.
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
