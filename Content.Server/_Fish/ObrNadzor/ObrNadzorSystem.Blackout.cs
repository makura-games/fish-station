using Content.Server.Power.Components;
using Content.Shared.Station.Components;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._Fish.ObrNadzor;

public sealed partial class ObrNadzorSystem
{
    private void BeginStationBlackout(EntityUid station, TimeSpan duration)
    {
        var largestGrid = _station.GetLargestGrid(station);
        if (largestGrid == null)
            return;

        var disabled = new List<EntityUid>();
        var query = AllEntityQuery<ApcComponent, TransformComponent>();
        while (query.MoveNext(out var apcUid, out var apc, out var xform))
        {
            if (!apc.MainBreakerEnabled)
                continue;

            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != station)
                continue;

            if (xform.GridUid != largestGrid.Value)
                continue;

            _apc.ApcToggleBreaker(apcUid, apc);
            disabled.Add(apcUid);
        }

        if (disabled.Count == 0)
            return;

        Timer.Spawn(duration, () => RestoreBlackout(disabled));
    }

    private void RestoreBlackout(List<EntityUid> disabled)
    {
        foreach (var apcUid in disabled)
        {
            if (!Exists(apcUid))
                continue;

            if (!TryComp<ApcComponent>(apcUid, out var apc))
                continue;

            // Включаем только то, что мы выключили и что всё ещё выключено.
            if (apc.MainBreakerEnabled)
                continue;

            _apc.ApcToggleBreaker(apcUid, apc);
        }
    }
}
