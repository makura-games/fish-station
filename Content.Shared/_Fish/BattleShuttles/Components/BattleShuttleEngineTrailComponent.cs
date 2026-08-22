using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.BattleShuttles.Components;

/// <summary>
/// Клиентский след двигателей боевого шаттла (аналог ion trail / JetpackEffect).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BattleShuttleEngineTrailComponent : Component
{
    [DataField]
    public float EffectCooldown = 0.25f;

    [DataField]
    public float MinSpeedSquared = 1f;

    [DataField]
    public EntProtoId EffectPrototype = "JetpackEffect";

    [ViewVariables]
    public EntityCoordinates LastCoordinates;

    [ViewVariables]
    public TimeSpan TargetTime = TimeSpan.Zero;
}
