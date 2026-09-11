using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Artillery;

[Serializable, NetSerializable]
public sealed partial class BluespaceArtillerySetCoordsMessage : BoundUserInterfaceMessage
{
    public ArtilleryVector2 Coordinates { get; set; }
}