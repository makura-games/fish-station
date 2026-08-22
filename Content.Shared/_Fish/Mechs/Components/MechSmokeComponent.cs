using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Timing;

namespace Content.Shared._Fish.Mechs.Components;

/// <summary>
/// Дымовая завеса с ограниченным числом зарядов.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class MechSmokeComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Charges = 5;

    [DataField]
    public int MaxCharges = 5;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(10);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextReady;

    [DataField]
    public EntProtoId SmokePrototype = "Smoke";

    [DataField]
    public int SpreadAmount = 16;

    [DataField]
    public float DurationSeconds = 12f;

    [DataField]
    public EntProtoId LaunchAction = "ActionMechLaunchSmoke";

    [DataField]
    public EntityUid? LaunchActionEntity;
}
