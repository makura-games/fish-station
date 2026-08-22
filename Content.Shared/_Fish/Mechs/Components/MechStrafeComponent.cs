using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Mechs.Components;

/// <summary>
/// Strafe: сохранять facing при боковом движении; доп. расход энергии.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechStrafeComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField]
    public float EnergyPerStep = 5f;

    [DataField]
    public float BackwardEnergyMultiplier = 2f;

    [DataField]
    public EntProtoId ToggleAction = "ActionMechToggleStrafe";

    [DataField]
    public EntityUid? ToggleActionEntity;
}
