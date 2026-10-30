using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Judicial Visor — smite по области с задержкой.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishJudicialVisorComponent : Component
{
    [DataField]
    public float CooldownSeconds = 30f;

    [DataField]
    public float SmiteDelaySeconds = 3f;

    [DataField]
    public float SmiteRadius = 2.5f;

    [DataField, AutoNetworkedField]
    public TimeSpan? NextUseTime;
}
