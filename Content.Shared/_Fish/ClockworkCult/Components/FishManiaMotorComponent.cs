using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Mania Motor — area denial / ментальные эффекты.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishManiaMotorComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField]
    public float Range = FishClockworkCultConstants.ManiaMotorRange;

    [DataField]
    public float PowerDrainPerSecond = 50f;

    [DataField]
    public float DamagePerTick = 2f;

    [DataField]
    public float TickInterval = 2f;

    [ViewVariables]
    public float Accumulator;
}
