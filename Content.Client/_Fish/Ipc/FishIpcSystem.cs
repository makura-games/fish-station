using Content.Shared._Fish.Ipc;
using Content.Shared.Alert;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Fish.Ipc;

/// <summary>
/// Клиентские алерты батареи КПБ.
/// </summary>
public sealed partial class FishIpcSystem : SharedFishIpcSystem
{
    [Dependency] private AlertsSystem _clientAlerts = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan AlertUpdateDelay = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextAlertUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishIpcComponent, LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<FishIpcComponent, LocalPlayerDetachedEvent>(OnDetached);
    }

    private void OnAttached(Entity<FishIpcComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        UpdateBatteryAlert(ent);
    }

    private void OnDetached(Entity<FishIpcComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        _clientAlerts.ClearAlert(ent.Owner, ent.Comp.BatteryAlert);
        _clientAlerts.ClearAlert(ent.Owner, ent.Comp.NoBatteryAlert);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_player.LocalEntity is not { } local)
            return;

        if (_timing.CurTime < _nextAlertUpdate)
            return;

        _nextAlertUpdate = _timing.CurTime + AlertUpdateDelay;

        if (!TryComp<FishIpcComponent>(local, out var ipc))
            return;

        UpdateBatteryAlert((local, ipc));
    }

    private void UpdateBatteryAlert(Entity<FishIpcComponent> ent)
    {
        if (!_powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery))
        {
            _clientAlerts.ShowAlert(ent.Owner, ent.Comp.NoBatteryAlert);
            return;
        }

        var chargeLevel = (short) MathF.Round(_battery.GetChargeLevel(battery.Value.AsNullable()) * 10f);
        if (chargeLevel == 0 && _powerCell.HasDrawCharge((ent.Owner, null, null)))
            chargeLevel = 1;

        _clientAlerts.ShowAlert(ent.Owner, ent.Comp.BatteryAlert, chargeLevel);
    }
}
