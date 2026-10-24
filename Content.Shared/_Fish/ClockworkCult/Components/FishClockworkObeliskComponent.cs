using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Clockwork Obelisk — Hierophant broadcast + Spatial Gateway hub.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishClockworkObeliskComponent : Component
{
    [DataField]
    public float GatewayPowerCost = 2000f;

    [DataField]
    public float BroadcastPowerCost = 50f;

    [DataField]
    public float GatewayLifetimeSeconds = 10f;

    [DataField]
    public int GatewayMaxUses = 5;

    [DataField, AutoNetworkedField]
    public int GatewayUsesRemaining = 5;

    [DataField, AutoNetworkedField]
    public bool AnchoredPowered = true;
}
