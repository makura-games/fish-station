using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Vision;

/// <summary>Настройки активного термального зрения от экипировки или имплантов.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishThermalVisionComponent : Component
{
    /// <summary>Maximum sampling and rendering distance in metres; never expands PVS.</summary>
    [DataField, AutoNetworkedField]
    public float Range = 16f;
}
