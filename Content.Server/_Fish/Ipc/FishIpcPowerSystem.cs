using Content.Shared._Fish.Ipc;
using Content.Shared.Alert;
using Content.Shared.Bed.Sleep;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Ipc;

/// <summary>
/// Серверная логика КПБ: расход через <see cref="PowerCellSystem.TryUseCharge"/>,
/// отключение при нуле, восстановление при зарядке.
/// </summary>
public sealed partial class FishIpcPowerSystem : SharedFishIpcSystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _moveMod = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SleepingSystem _sleep = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextWarn;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishIpcComponent, PowerCellSlotEmptyEvent>(OnPowerCellEmpty);
        SubscribeLocalEvent<FishIpcComponent, EntInsertedIntoContainerMessage>(OnCellInserted);
        SubscribeLocalEvent<FishIpcComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnTerminating(Entity<FishIpcComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!ent.Comp.PowerDown)
            return;

        ent.Comp.PowerDown = false;
        _statusEffects.TryRemoveStatusEffect(ent.Owner, SleepingSystem.StatusEffectForcedSleeping);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FishIpcComponent, PowerCellSlotComponent>();
        while (query.MoveNext(out var uid, out var ipc, out var slot))
        {
            // Cell removal during RecursiveDeleteEntity raises empty/slot events and would
            // EnsureComp StatusEffectContainer on a terminating MobIpc.
            if (TerminatingOrDeleted(uid) || _mobState.IsDead(uid) || !ipc.BatteryPowered)
                continue;

            // SSD не расходуют заряд и не получают предупреждения.
            if (TryComp<MindContainerComponent>(uid, out var mind) && !mind.HasMind)
                continue;

            if (!_powerCell.TryGetBatteryFromSlot((uid, slot), out var batteryEnt))
            {
                if (_alerts.IsShowingAlert(uid, ipc.BatteryAlert))
                {
                    _alerts.ClearAlert(uid, ipc.BatteryAlert);
                    _alerts.ShowAlert(uid, ipc.NoBatteryAlert);
                }

                UpdateChargeState(uid, ipc, 0);
                continue;
            }

            if (!ipc.PowerDown)
                _powerCell.TryUseCharge((uid, slot), frameTime * ipc.DrainPerSecond);

            var level = _battery.GetChargeLevel(batteryEnt.Value.AsNullable());
            var chargePercent = (short) MathF.Round(level * 10f);
            if (chargePercent == 0 && level > 0.001f)
                chargePercent = 1;

            UpdateChargeState(uid, ipc, chargePercent);

            if (_alerts.IsShowingAlert(uid, ipc.NoBatteryAlert) && chargePercent > 0)
            {
                _alerts.ClearAlert(uid, ipc.NoBatteryAlert);
                _alerts.ShowAlert(uid, ipc.BatteryAlert, chargePercent);
            }

            if (level <= ipc.ChargeThresholdCritical && level > 0f && _timing.CurTime >= _nextWarn)
            {
                _nextWarn = _timing.CurTime + TimeSpan.FromSeconds(20);
                _popup.PopupEntity(Loc.GetString("robot-charge-low-percent", ("percent", (int)(level * 100))), uid, uid);
            }
        }
    }

    private void OnPowerCellEmpty(Entity<FishIpcComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        if (TerminatingOrDeleted(ent.Owner))
            return;

        UpdateChargeState(ent.Owner, ent.Comp, 0);
    }

    private void OnCellInserted(Entity<FishIpcComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (TerminatingOrDeleted(ent.Owner))
            return;

        if (!TryComp<PowerCellSlotComponent>(ent.Owner, out var slot) || args.Container.ID != slot.CellSlotId)
            return;

        if (_powerCell.TryGetBatteryFromSlot((ent.Owner, slot), out var battery))
        {
            var level = _battery.GetChargeLevel(battery.Value.AsNullable());
            UpdateChargeState(ent.Owner, ent.Comp, (short) MathF.Round(level * 10f));
        }
    }

    private void UpdateChargeState(EntityUid uid, FishIpcComponent ipc, short chargePercent)
    {
        if (TerminatingOrDeleted(uid))
            return;

        if (ipc.ChargeState != chargePercent)
        {
            RaiseLocalEvent(uid, new FishIpcChargeStateUpdateEvent(chargePercent));
            _moveMod.RefreshMovementSpeedModifiers(uid);
        }

        if (chargePercent == 0 && !ipc.PowerDown)
            PowerDown(uid, ipc);
        else if (chargePercent > 0 && ipc.PowerDown)
            PowerUp(uid, ipc);
    }

    private void PowerDown(EntityUid uid, FishIpcComponent ipc)
    {
        if (TerminatingOrDeleted(uid))
            return;

        ipc.PowerDown = true;
        Dirty(uid, ipc);

        // Постоянный forced sleep до появления заряда.
        _statusEffects.TrySetStatusEffectDuration(uid, SleepingSystem.StatusEffectForcedSleeping, duration: null);
        _sleep.TrySleeping(uid);
        _popup.PopupEntity(Loc.GetString("silicon-power-low"), uid, uid);
    }

    private void PowerUp(EntityUid uid, FishIpcComponent ipc)
    {
        if (TerminatingOrDeleted(uid))
            return;

        ipc.PowerDown = false;
        Dirty(uid, ipc);
        _statusEffects.TryRemoveStatusEffect(uid, SleepingSystem.StatusEffectForcedSleeping);
        _sleep.TryWaking(uid, true);
    }
}
