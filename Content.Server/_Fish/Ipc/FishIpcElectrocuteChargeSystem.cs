using Content.Shared._Fish.Ipc;
using Content.Shared.Electrocution;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;

namespace Content.Server._Fish.Ipc;

/// <summary>Шок частично заряжает батарею КПБ (Paradise spec_electrocute_act).</summary>
public sealed partial class FishIpcElectrocuteChargeSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private Robust.Shared.Random.IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishIpcComponent, ElectrocutedEvent>(OnElectrocuted);
    }

    private void OnElectrocuted(EntityUid uid, FishIpcComponent component, ElectrocutedEvent args)
    {
        if (!component.BatteryPowered || !_powerCell.TryGetBatteryFromSlot(uid, out var batteryEnt))
            return;

        var (charge, max) = _battery.GetCharge(batteryEnt.Value.Owner);
        var add = Math.Min(
            50f * Math.Max(args.SiemensCoefficient, 0.1f) * _random.NextFloat(0.75f, 1.25f),
            max * 0.25f);

        if (add <= 0 || charge >= max)
            return;

        _battery.SetCharge(batteryEnt.Value.AsNullable(), Math.Min(max, charge + add));
        _popup.PopupEntity(Loc.GetString("fish-ipc-electrocute-charge"), uid, uid);
    }
}
