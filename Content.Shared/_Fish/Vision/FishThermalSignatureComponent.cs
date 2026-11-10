using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Vision;

/// <summary>Entity temperature delivered through ordinary PVS only to eligible thermal viewers.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishThermalSignatureComponent : Component
{
    /// <inheritdoc/>
    public override bool SessionSpecific => true;

    /// <summary>Server-measured temperature in kelvin, quantized to limit network changes.</summary>
    [AutoNetworkedField]
    public float Temperature;
}
