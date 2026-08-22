using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Mechs.Components;

/// <summary>
/// Dual-hand: primary = MechComponent.CurrentSelectedEquipment, secondary хранится здесь.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechDualEquipmentComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? SecondarySelectedEquipment;

    [DataField]
    public EntProtoId SwapAction = "ActionMechSwapEquipmentHands";

    [DataField]
    public EntityUid? SwapActionEntity;
}
