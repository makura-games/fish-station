using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Eminence — особый слуга с mass recall.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishEminenceComponent : Component
{
    [DataField]
    public EntProtoId MassRecallAction = "FishActionEminenceMassRecall";

    [DataField, AutoNetworkedField]
    public EntityUid? MassRecallActionEntity;
}
