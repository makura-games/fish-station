using Content.Shared._Fish.Ipc;
using Content.Shared._Fish.Ipc.Modules;
using Content.Shared.Alert;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Ipc;

/// <summary>
/// Серверная логика КПБ: расход через <see cref="PowerCellSystem.TryUseCharge"/>,
/// замедление и отключение рации при нуле, восстановление при зарядке.
/// </summary>
public sealed partial class FishIpcPowerSystem : SharedFishIpcSystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _moveMod = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private FishIpcRadioSystem _ipcRadio = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>Следующее предупреждение о критическом заряде по каждому КПБ.</summary>
    private readonly Dictionary<EntityUid, TimeSpan> _nextWarn = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishIpcComponent, PowerCellSlotEmptyEvent>(OnPowerCellEmpty);
        SubscribeLocalEvent<FishIpcComponent, EntInsertedIntoContainerMessage>(OnCellInserted);
        SubscribeLocalEvent<FishIpcComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<FishIpcComponent> ent, ref ComponentShutdown args)
    {
        _nextWarn.Remove(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FishIpcComponent, PowerCellSlotComponent>();
        while (query.MoveNext(out var uid, out var ipc, out var slot))
        {
            // Снятие ячейки при удалении сущности не должно трогать заряд и рацию.
            if (TerminatingOrDeleted(uid) || !ipc.BatteryPowered)
                continue;

            // Мёртвые и SSD не тратят заряд, но индикатор всё равно читает ту же ячейку, что и осмотр.
            var skipDrain = _mobState.IsDead(uid)
                || (TryComp<MindContainerComponent>(uid, out var mind) && !mind.HasMind);

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

            if (!skipDrain && !ipc.PowerDown)
            {
                ipc.ChassisDrainTimer += frameTime;
                if (ipc.ChassisDrainTimer >= 1f)
                {
                    var seconds = MathF.Floor(ipc.ChassisDrainTimer);
                    ipc.ChassisDrainTimer -= seconds;
                    _powerCell.TryUseCharge((uid, slot), seconds * ipc.DrainPerSecond);
                }
            }

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

            if (skipDrain)
                continue;

            if (level <= ipc.ChargeThresholdCritical && level > 0f)
            {
                var now = _timing.CurTime;
                if (!_nextWarn.TryGetValue(uid, out var next) || now >= next)
                {
                    _nextWarn[uid] = now + TimeSpan.FromSeconds(20);
                    // round(level*100) при крошечном заряде даёт 0, хотя шкала уже 1.
                    var shownPercent = (int) MathF.Round(level * 100f);
                    if (shownPercent < 1)
                        shownPercent = 1;
                    _popup.PopupEntity(Loc.GetString("robot-charge-low-percent", ("percent", shownPercent)), uid, uid);
                }
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
        _ipcRadio.SetRadioEnabled((uid, ipc), false);
        _moveMod.RefreshMovementSpeedModifiers(uid);
        _popup.PopupEntity(Loc.GetString("silicon-power-low"), uid, uid);

        var ev = new FishIpcChassisPowerChangedEvent(false);
        RaiseLocalEvent(uid, ref ev);
    }

    private void PowerUp(EntityUid uid, FishIpcComponent ipc)
    {
        if (TerminatingOrDeleted(uid))
            return;

        ipc.PowerDown = false;
        Dirty(uid, ipc);
        _ipcRadio.SetRadioEnabled((uid, ipc), true);
        _moveMod.RefreshMovementSpeedModifiers(uid);

        var ev = new FishIpcChassisPowerChangedEvent(true);
        RaiseLocalEvent(uid, ref ev);
    }
}
