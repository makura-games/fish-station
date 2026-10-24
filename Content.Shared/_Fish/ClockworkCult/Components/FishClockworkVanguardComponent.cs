using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Временный щит Vanguard — поглощение станов.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishClockworkVanguardComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndsAt;

    [DataField, AutoNetworkedField]
    public float AbsorbedStunSeconds;
}
