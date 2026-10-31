using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Integration Cog — вставляется в APC и сливает мощность в пул культа.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishIntegrationCogComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Installed;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? HostApc;

    [DataField]
    public float DrainWattsPerSecond = FishClockworkCultConstants.IntegrationCogDrainWattsPerSecond;
}
