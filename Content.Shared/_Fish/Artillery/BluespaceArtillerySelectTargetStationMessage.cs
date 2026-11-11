using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Artillery;

[Serializable, NetSerializable]
public sealed class BluespaceArtillerySelectTargetStationMessage : BoundUserInterfaceMessage
{
    public NetEntity Station { get; }

    public BluespaceArtillerySelectTargetStationMessage(NetEntity station)
    {
        Station = station;
    }
}
