using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Eminence Spire — выбор Eminence среди слуг.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishEminenceSpireComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? CurrentEminence;

    [ViewVariables]
    public Dictionary<EntityUid, EntityUid> Votes = new();
}
