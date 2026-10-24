using Content.Shared.Alert;
using Content.Shared.Bed.Sleep;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Movement.Systems;
using Content.Shared.PowerCell.Components;

namespace Content.Shared._Fish.Ipc;

/// <summary>
/// Общая логика КПБ: алерты, скорость, запрет самообслуживания слота батареи, сон.
/// </summary>
public abstract partial class SharedFishIpcSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishIpcComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<FishIpcComponent, FishIpcChargeStateUpdateEvent>(OnChargeUpdate);
        SubscribeLocalEvent<FishIpcComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
        SubscribeLocalEvent<FishIpcComponent, ItemSlotInsertAttemptEvent>(OnItemSlotInsertAttempt);
        SubscribeLocalEvent<FishIpcComponent, ItemSlotEjectAttemptEvent>(OnItemSlotEjectAttempt);
        SubscribeLocalEvent<FishIpcComponent, TryingToSleepEvent>(OnTryingToSleep);
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
        if (args.Cancelled || args.User != ent.Owner)
            return;

        if (!TryComp<PowerCellSlotComponent>(ent.Owner, out var cellSlot) ||
            !_itemSlots.TryGetSlot(ent.Owner, cellSlot.CellSlotId, out var slot) ||
            slot != args.Slot)
            return;

        args.Cancelled = true;
    }

    private void OnItemSlotEjectAttempt(Entity<FishIpcComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Cancelled || args.User != ent.Owner)
            return;

        if (!TryComp<PowerCellSlotComponent>(ent.Owner, out var cellSlot) ||
            !_itemSlots.TryGetSlot(ent.Owner, cellSlot.CellSlotId, out var slot) ||
            slot != args.Slot)
            return;

        args.Cancelled = true;
    }

    private void OnTryingToSleep(Entity<FishIpcComponent> ent, ref TryingToSleepEvent args)
    {
        if (!ent.Comp.AllowSleep && !ent.Comp.PowerDown)
            args.Cancelled = true;
    }
}
