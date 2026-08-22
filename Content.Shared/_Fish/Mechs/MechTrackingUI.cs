using Content.Shared.Mech.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Mechs;

[Serializable, NetSerializable]
public enum MechTrackingUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class MechTrackingEntry
{
    public NetEntity Mech;
    public string Name = string.Empty;
    public float IntegrityPercent;
    public float EnergyPercent;
    public string PilotName = string.Empty;
    public bool Broken;
}

[Serializable, NetSerializable]
public sealed class MechTrackingBoundUserInterfaceState : BoundUserInterfaceState
{
    public List<MechTrackingEntry> Entries = [];
}

[Serializable, NetSerializable]
public sealed class MechTrackingRefreshMessage : BoundUserInterfaceMessage;
