using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Mechs.Components;

/// <summary>
/// Tracking beacon: мех виден на robotics/mech tracking console.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechTrackingBeaconComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;
}
