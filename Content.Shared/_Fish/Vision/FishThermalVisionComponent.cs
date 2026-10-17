using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Vision;

/// <summary>Active thermal optics granted by existing clothing and battery controls.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishThermalVisionComponent : Component
{
    /// <summary>Maximum sampling and rendering distance in metres; never expands PVS.</summary>
    [DataField, AutoNetworkedField]
    public float Range = 16f;
}
