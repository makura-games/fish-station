using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Mechs.Components;

/// <summary>
/// Phazon phase: отключает hard-collision фикстур, расход энергии.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechPhasingComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField]
    public float EnergyPerSecond = 15f;

    [DataField]
    public float SpeedMultiplier = 0.35f;

    [DataField]
    public EntProtoId ToggleAction = "ActionMechTogglePhasing";

    [DataField]
    public EntityUid? ToggleActionEntity;
}
