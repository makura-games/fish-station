using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Mechs.Components;

/// <summary>
/// Режим обороны: якорь на месте + снижение входящего урона (не бонус deflect).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechDefenceModeComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    /// <summary>
    /// Доля урона, снимаемая в режиме обороны (0.3 = −30% входящего).
    /// </summary>
    [DataField]
    public float DamageResistFraction = 0.3f;

    [DataField]
    public EntProtoId ToggleAction = "ActionMechToggleDefence";

    [DataField]
    public EntityUid? ToggleActionEntity;
}
