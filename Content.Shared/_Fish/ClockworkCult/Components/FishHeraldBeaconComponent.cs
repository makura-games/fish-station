using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Herald's Beacon — объявление войны и 25% скидка на мощность.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishHeraldBeaconComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Activated;

    [DataField]
    public float VoteDurationSeconds = 60f;

    [ViewVariables]
    public HashSet<EntityUid> VotesYes = new();

    [ViewVariables]
    public HashSet<EntityUid> VotesNo = new();

    [ViewVariables]
    public TimeSpan? VoteEndsAt;
}
