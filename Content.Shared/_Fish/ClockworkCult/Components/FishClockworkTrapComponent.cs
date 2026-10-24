using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Ловушка Clockwork Cult (skewer / vent / sensor / lever / repeater).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishClockworkTrapComponent : Component
{
    [DataField, AutoNetworkedField]
    public FishClockworkTrapKind Kind = FishClockworkTrapKind.Generic;

    [DataField]
    public bool ConsumedOnTrigger = true;

    [DataField]
    public float TriggerRange = 1f;

    [DataField]
    public float SkewerDamage = 25f;

    [DataField]
    public float SkewerStunSeconds = 3f;

    [DataField]
    public float SteamDurationSeconds = 4f;

    [DataField]
    public float CooldownSeconds = 5f;

    [ViewVariables]
    public TimeSpan? NextTriggerTime;
}
