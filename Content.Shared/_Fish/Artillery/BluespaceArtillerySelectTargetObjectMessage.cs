using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Artillery;

[Serializable, NetSerializable]
public sealed class BluespaceArtillerySelectTargetObjectMessage : BoundUserInterfaceMessage
{
    public NetEntity? TargetObject { get; }

    public BluespaceArtillerySelectTargetObjectMessage(NetEntity? targetObject)
    {
        TargetObject = targetObject;
    }
}
