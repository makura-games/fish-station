using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Artillery;

[Serializable, NetSerializable]
public enum BluespaceArtilleryVisuals : byte
{
    VisualState
}

[Serializable, NetSerializable]
public enum BluespaceArtilleryVisualState : byte
{
    Idle,
    Charging,
    Fire
}