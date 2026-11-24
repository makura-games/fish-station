using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Homelander;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class HomelanderFlightComponent : Component
{
    /// <summary>Prototype of the action that toggles flight.</summary>
    [DataField]
    public EntProtoId Action = "FishActionHomelanderFlight";

    /// <summary>Movement speed multiplier while airborne.</summary>
    [DataField]
    public float SpeedModifier = 1.85f;

    /// <summary>Friction multiplier while airborne, including in open space.</summary>
    [DataField]
    public float FrictionModifier = 0.55f;

    /// <summary>Whether flight is active on the server and replicated to clients.</summary>
    [AutoNetworkedField]
    public bool FlightEnabled;

    /// <summary>Server-side instance of the action granted to this entity.</summary>
    public EntityUid? ActionEntity;
}
