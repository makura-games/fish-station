using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Mechs.Components;

/// <summary>
/// DNA-замок входа: если задан LockedDna, пилот должен совпасть по DnaComponent.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechDnaLockComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? LockedDna;

    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField]
    public EntProtoId SetDnaAction = "ActionMechSetDnaLock";

    [DataField]
    public EntProtoId ClearDnaAction = "ActionMechClearDnaLock";

    [DataField]
    public EntityUid? SetDnaActionEntity;

    [DataField]
    public EntityUid? ClearDnaActionEntity;
}
