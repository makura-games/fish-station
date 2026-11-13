using System.Linq;
using Content.Shared.Bed.Sleep;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Lock;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.PowerCell.Components;
using Content.Shared.Radio.Components;
using Content.Shared.Radio.EntitySystems;
using Content.Shared.Tools.Components;
using Content.Shared.Wires;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using SharedToolSystem = Content.Shared.Tools.Systems.SharedToolSystem;

namespace Content.Shared._Fish.Ipc;

/// <summary>
/// Общая логика КПБ: заряд и скорость, слот батареи/ключей, сон, self-unlock шасси.
/// Иконку заряда рисует клиент: BorgBattery помечен clientHandled.
/// </summary>
public abstract partial class SharedFishIpcSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private LockSystem _lock = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishIpcComponent, FishIpcChargeStateUpdateEvent>(OnChargeUpdate);
        SubscribeLocalEvent<FishIpcComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
        SubscribeLocalEvent<FishIpcComponent, ItemSlotInsertAttemptEvent>(OnItemSlotInsertAttempt);
        SubscribeLocalEvent<FishIpcComponent, ItemSlotEjectAttemptEvent>(OnItemSlotEjectAttempt);
        SubscribeLocalEvent<FishIpcComponent, TryingToSleepEvent>(OnTryingToSleep);
        // Fish-edit: только сам себе — чужих КПБ без Command/Research/Robotics не открываем
        SubscribeLocalEvent<FishIpcComponent, CheckUserHasLockAccessEvent>(OnCheckLockAccess);
        // Fish-edit: батарея/ключи до ItemSlots/EncryptionKeySystem (тот же гейт, что модули)
        SubscribeLocalEvent<FishIpcComponent, InteractUsingEvent>(
            OnInteractUsing,
            before: [typeof(ItemSlotsSystem), typeof(EncryptionKeySystem)]);
    }

    private void OnChargeUpdate(Entity<FishIpcComponent> ent, ref FishIpcChargeStateUpdateEvent args)
    {
        ent.Comp.ChargeState = args.ChargePercent;
        Dirty(ent);
    }

    private void OnRefreshMovespeed(Entity<FishIpcComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var closest = 0;
        foreach (var (state, _) in ent.Comp.SpeedModifierThresholds)
        {
            if (ent.Comp.ChargeState >= state && state > closest)
                closest = state;
        }

        if (!ent.Comp.SpeedModifierThresholds.TryGetValue(closest, out var speedMod))
            return;

        args.ModifySpeed(speedMod, speedMod);
    }

    private void OnInteractUsing(Entity<FishIpcComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<PowerCellComponent>(args.Used))
        {
            args.Handled = true;
            TrySwapPowerCell(ent.Owner, args.User, args.Used);
            return;
        }

        if (HasComp<EncryptionKeyComponent>(args.Used))
        {
            args.Handled = true;
            TryInsertEncryptionKey(ent.Owner, args.User, args.Used);
            return;
        }

        // Отвёртка достаёт ключи сразу: штатный EncKeySystem + DoAfter на живом шасси с Wires ломается.
        if (!TryComp<EncryptionKeyHolderComponent>(ent.Owner, out var holder) ||
            holder.KeyContainer.ContainedEntities.Count == 0 ||
            !TryComp<ToolComponent>(args.Used, out var tool) ||
            !_tool.HasQuality(args.Used, holder.KeysExtractionMethod, tool))
            return;

        args.Handled = true;
        TryExtractEncryptionKeys(ent.Owner, args.User, holder);
    }

    private void OnItemSlotInsertAttempt(Entity<FishIpcComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled || !IsCellSlot(ent.Owner, args.Slot))
            return;

        if (!CanAccessInternals(ent.Owner, args.User))
            args.Cancelled = true;
    }

    private void OnItemSlotEjectAttempt(Entity<FishIpcComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Cancelled || !IsCellSlot(ent.Owner, args.Slot))
            return;

        if (!CanAccessInternals(ent.Owner, args.User))
            args.Cancelled = true;
    }

    private bool IsCellSlot(EntityUid owner, ItemSlot slot)
    {
        if (!TryComp<PowerCellSlotComponent>(owner, out var cellSlot) ||
            !_itemSlots.TryGetSlot(owner, cellSlot.CellSlotId, out var owned) ||
            owned != slot)
            return false;

        return true;
    }

    /// <summary>
    /// Как модули: корпус разблокирован и панель открыта. LockedWiresPanel не даёт открыть панель при замке.
    /// </summary>
    private bool CanAccessInternals(EntityUid owner, EntityUid? user, string? panelClosedPopup = null)
    {
        if (_lock.IsLocked(owner))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("fish-ipc-module-locked"), owner, user.Value);
            return false;
        }

        if (TryComp<WiresPanelComponent>(owner, out var panel) && !panel.Open)
        {
            if (user != null)
            {
                _popup.PopupEntity(
                    Loc.GetString(panelClosedPopup ?? "fish-ipc-cell-panel-closed"),
                    owner,
                    user.Value);
            }

            return false;
        }

        return true;
    }

    private bool TrySwapPowerCell(EntityUid ipc, EntityUid user, EntityUid cell)
    {
        if (!CanAccessInternals(ipc, user))
            return false;

        if (!TryComp<PowerCellSlotComponent>(ipc, out var cellSlot) ||
            !_itemSlots.TryGetSlot(ipc, cellSlot.CellSlotId, out var slot))
            return false;

        if (!_itemSlots.CanInsert(ipc, slot, cell, user, swap: true))
            return false;

        if (slot.HasItem && !_itemSlots.TryEjectToHands(ipc, slot, user, excludeUserAudio: true))
            return false;

        return _itemSlots.TryInsert(ipc, slot, cell, user, excludeUserAudio: true);
    }

    private bool TryInsertEncryptionKey(EntityUid ipc, EntityUid user, EntityUid key)
    {
        if (!CanAccessInternals(ipc, user, "encryption-keys-panel-locked"))
            return false;

        if (!TryComp<EncryptionKeyHolderComponent>(ipc, out var holder))
            return false;

        if (!holder.KeysUnlocked)
        {
            _popup.PopupEntity(Loc.GetString("encryption-keys-are-locked"), ipc, user);
            return false;
        }

        if (holder.KeySlots <= holder.KeyContainer.ContainedEntities.Count)
        {
            _popup.PopupEntity(Loc.GetString("encryption-key-slots-already-full"), ipc, user);
            return false;
        }

        if (!_containers.Insert(key, holder.KeyContainer))
            return false;

        _popup.PopupEntity(Loc.GetString("encryption-key-successfully-installed"), ipc, user);
        _audio.PlayPredicted(holder.KeyInsertionSound, ipc, user);
        return true;
    }

    private void TryExtractEncryptionKeys(EntityUid ipc, EntityUid user, EncryptionKeyHolderComponent holder)
    {
        if (!CanAccessInternals(ipc, user, "encryption-keys-panel-locked"))
            return;

        if (!holder.KeysUnlocked)
        {
            _popup.PopupEntity(Loc.GetString("encryption-keys-are-locked"), ipc, user);
            return;
        }

        var contained = holder.KeyContainer.ContainedEntities.ToArray();
        _containers.EmptyContainer(holder.KeyContainer, reparent: false);
        foreach (var key in contained)
            _hands.PickupOrDrop(user, key, dropNear: true);

        _popup.PopupEntity(Loc.GetString("encryption-keys-all-extracted"), ipc, user);
        _audio.PlayPredicted(holder.KeyExtractionSound, ipc, user);
    }

    private void OnCheckLockAccess(Entity<FishIpcComponent> ent, ref CheckUserHasLockAccessEvent args)
    {
        if (!args.FoundReaders.HasFlag(LockTypes.Access))
            return;

        // Только свой корпус. Чужой КПБ — через AccessReader (Command/Research/Robotics).
        if (args.User == ent.Owner)
            args.HasAccess |= LockTypes.Access;
    }

    private void OnTryingToSleep(Entity<FishIpcComponent> ent, ref TryingToSleepEvent args)
    {
        if (!ent.Comp.AllowSleep)
            args.Cancelled = true;
    }
}
