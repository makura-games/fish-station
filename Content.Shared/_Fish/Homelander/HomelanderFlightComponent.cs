using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Homelander;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class HomelanderFlightComponent : Component
{
    [DataField]
    public EntProtoId Action = "FishActionHomelanderFlight";

    [DataField]
    public float SpeedModifier = 1.85f;

    [DataField]
    public float FrictionModifier = 0.55f;

    [AutoNetworkedField]
    public bool FlightEnabled;

    public EntityUid? ActionEntity;
}
