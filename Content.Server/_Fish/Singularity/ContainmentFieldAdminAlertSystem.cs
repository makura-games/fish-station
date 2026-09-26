using Content.Server.Chat.Managers;
using Content.Shared.Singularity.Components;
using Content.Shared.Station;

namespace Content.Server._Fish.Singularity;

/// <summary>
/// Sends a one-shot admin alert when a containment field generator discharges to 50% or below.
/// Re-alerts only after the charge recovers above the threshold and drops again.
/// </summary>
public sealed partial class ContainmentFieldAdminAlertSystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private SharedStationSystem _station = default!;

    private readonly HashSet<EntityUid> _wasAboveThreshold = new();
    private readonly HashSet<EntityUid> _alertedLowCharge = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ContainmentFieldGeneratorComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ContainmentFieldGeneratorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var generator, out var xform))
        {
            if (!ContainmentFieldChargeAlertLogic.TryRaiseAlert(
                    uid,
                    generator.PowerBuffer,
                    _wasAboveThreshold,
                    _alertedLowCharge))
            {
                continue;
            }

            SendLowChargeAlert(uid, xform, generator.PowerBuffer);
        }
    }

    private void OnShutdown(Entity<ContainmentFieldGeneratorComponent> ent, ref ComponentShutdown args)
    {
        _wasAboveThreshold.Remove(ent);
        _alertedLowCharge.Remove(ent);
    }

    private void SendLowChargeAlert(EntityUid uid, TransformComponent xform, int powerBuffer)
    {
        var chargePercent = (int)MathF.Round(ContainmentFieldChargeAlertLogic.GetChargePercent(powerBuffer));

        var gridName = xform.GridUid is { } grid && !TerminatingOrDeleted(grid)
            ? ToPrettyString(grid)
            : Loc.GetString("containment-field-admin-unknown-grid");

        var stationUid = _station.GetOwningStation(uid, xform);
        var stationName = stationUid is { } station && !TerminatingOrDeleted(station)
            ? ToPrettyString(station)
            : Loc.GetString("containment-field-admin-unknown-station");

        _chat.SendAdminAlert(Loc.GetString(
            "containment-field-admin-low-charge",
            ("name", ToPrettyString(uid)),
            ("charge", chargePercent),
            ("coordinates", xform.Coordinates),
            ("grid", gridName),
            ("station", stationName)));
    }
}
