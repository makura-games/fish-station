using Robust.Shared.Serialization;

namespace Content.Shared.Holopad;

[Serializable, NetSerializable]
public sealed class RemoteHolopadStopBroadcastMessage : BoundUserInterfaceMessage
{
}