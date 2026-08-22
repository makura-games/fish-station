using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Mechs.Components;

/// <summary>
/// Zoom шасси (Marauder): увеличивает ContentEye пилота и блокирует движение.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechZoomComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField]
    public Vector2 Zoom = new(1.5f, 1.5f);

    [DataField]
    public EntProtoId ToggleAction = "ActionMechToggleZoom";

    [DataField]
    public EntityUid? ToggleActionEntity;
}
