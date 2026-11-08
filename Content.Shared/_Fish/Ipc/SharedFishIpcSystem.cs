using Content.Shared.Alert;
using Content.Shared.Bed.Sleep;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Lock;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.PowerCell.Components;
using Content.Shared.Wires;

namespace Content.Shared._Fish.Ipc;

/// <summary>
/// Общая логика КПБ: алерты, скорость, слот батареи, сон, взаимный unlock шасси.
/// </summary>
public abstract partial class SharedFishIpcSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishIpcComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<FishIpcComponent, FishIpcChargeStateUpdateEvent>(OnChargeUpdate);
        SubscribeLocalEvent<FishIpcComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
        SubscribeLocalEvent<FishIpcComponent, ItemSlotInsertAttemptEvent>(OnItemSlotInsertAttempt);
        SubscribeLocalEvent<FishIpcComponent, ItemSlotEjectAttemptEvent>(OnItemSlotEjectAttempt);
        SubscribeLocalEvent<FishIpcComponent, TryingToSleepEvent>(OnTryingToSleep);
        // Fish-edit: любой КПБ открывает замок другого КПБ
        SubscribeLocalEvent<FishIpcComponent, CheckUserHasLockAccessEvent>(OnCheckLockAccess);
    }

    private void OnInit(Entity<FishIpcComponent> ent, ref ComponentInit args)
    {
        _alerts.ShowAlert(ent.Owner, ent.Comp.BatteryAlert, ent.Comp.ChargeState);
    }

    private void OnChargeUpdate(Entity<FishIpcComponent> ent, ref FishIpcChargeStateUpdateEvent args)
    {
        ent.Comp.ChargeState = args.ChargePercent;
        Dirty(ent);
        _alerts.ShowAlert(ent.Owner, ent.Comp.BatteryAlert, args.ChargePercent);
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

    private void OnItemSlotInsertAttempt(Entity<FishIpcComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled || !IsCellSlot(ent.Owner, args.Slot))
            return;

        // Как модули: самообслуживание ок при открытой панели (LockedWiresPanel уже требует unlock).
        if (!CanSwapCell(ent.Owner, args.User))
            args.Cancelled = true;
    }

    private void OnItemSlotEjectAttempt(Entity<FishIpcComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Cancelled || !IsCellSlot(ent.Owner, args.Slot))
            return;

        if (!CanSwapCell(ent.Owner, args.User))
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
    /// Батарею меняют при открытой панели (как у боргов). Замок отдельно не проверяем:
    /// LockedWiresPanel не даёт открыть панель, пока корпус заперт.
    /// </summary>
    private bool CanSwapCell(EntityUid owner, EntityUid? user)
    {
        if (TryComp<WiresPanelComponent>(owner, out var panel) && !panel.Open)
        {
            if (user != null)
                _popup.PopupClient(Loc.GetString("fish-ipc-cell-panel-closed"), owner, user.Value);
            return false;
        }

        return true;
    }

    private void OnCheckLockAccess(Entity<FishIpcComponent> ent, ref CheckUserHasLockAccessEvent args)
    {
        if (!args.FoundReaders.HasFlag(LockTypes.Access))
            return;

        // Peer-unlock: КПБ ↔ КПБ без ID с тегом FishIpcChassis.
        if (HasComp<FishIpcComponent>(args.User))
            args.HasAccess |= LockTypes.Access;
    }

    private void OnTryingToSleep(Entity<FishIpcComponent> ent, ref TryingToSleepEvent args)
    {
        if (!ent.Comp.AllowSleep)
            args.Cancelled = true;
    }
}
