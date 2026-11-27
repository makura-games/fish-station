using Robust.Shared.Serialization;
using static Content.Shared.Pinpointer.SharedNavMapSystem;

namespace Content.Shared._Fish.ObrNadzor;

[Serializable, NetSerializable]
public enum ObrNadzorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ObrNadzorBuiState : BoundUserInterfaceState
{
    public required Dictionary<int, ObrNadzorStationBeacons> Stations { get; init; }

    public bool Observing { get; init; }

    public bool SessionActive { get; init; }
}

[Serializable, NetSerializable]
public sealed class ObrNadzorStationBeacons
{
    public required int StationId { get; init; }
    public required string Name { get; init; }
    public required List<NavMapBeacon> Beacons { get; init; }
}

[Serializable, NetSerializable]
public sealed class ObrNadzorBeaconChosenBuiMsg : BoundUserInterfaceMessage
{
    public required NavMapBeacon Beacon { get; init; }

    public int StationId { get; init; }
}

[Serializable, NetSerializable]
public sealed class ObrNadzorTakeoverBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ObrNadzorStopObserveBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ObrNadzorReleaseSessionBuiMsg : BoundUserInterfaceMessage;
