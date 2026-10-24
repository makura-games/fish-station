using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Маркер APC с установленной Integration Cog — drain идёт на хосте, а не на предмете.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishIntegrationCogHostComponent : Component
{
    [DataField, AutoNetworkedField]
    public float DrainWattsPerSecond = FishClockworkCultConstants.IntegrationCogDrainWattsPerSecond;
}
