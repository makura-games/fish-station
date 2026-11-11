using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Homelander;

[RegisterComponent, NetworkedComponent]
public sealed partial class HomelanderComponent : Component
{
    public const float FlightVisualLift = 0.48f;

    public EntityUid? ScreamAction;
    public EntityUid? LaserAction;
    public EntityUid? Laser;
}

public sealed partial class HomelanderScreamEvent : InstantActionEvent;

[Serializable, NetSerializable]
public enum HomelanderVisuals : byte
{
    LaserActive,
    Anger,
}

[Serializable, NetSerializable]
public sealed class HomelanderFireEvent(NetCoordinates coordinates, NetEntity? target) : EntityEventArgs
{
    public NetCoordinates Coordinates { get; } = coordinates;
    public NetEntity? Target { get; } = target;
}
