using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Artillery;

[Serializable, NetSerializable]
public sealed partial class BluespaceArtilleryPreviewMessage : BoundUserInterfaceMessage
{
    public bool Enabled { get; set; }
}