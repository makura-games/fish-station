using Content.Shared.Pinpointer;

namespace Content.Server.Pinpointer;

public sealed partial class NavMapSystem
{
    // Восстанавливает только маяки перенесённого грида, сохраняя существующие данные карты.

    /// <summary>Refreshes transferred beacon entries after welding has reparented them to the surviving grid.</summary>
    internal void RefreshGridWeldingBeacons(IReadOnlyList<EntityUid> beacons)
    {
        foreach (var uid in beacons)
        {
            if (!TerminatingOrDeleted(uid) && TryComp<NavMapBeaconComponent>(uid, out var beacon))
                UpdateNavMapBeaconData(uid, beacon);
        }
    }
}
