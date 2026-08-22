using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Mechs.Components;

/// <summary>
/// Перегрузка ног (Gygax): скорость↑, расход энергии↑, self-damage при движении.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechOverloadComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField]
    public float SpeedMultiplier = 1.75f;

    [DataField]
    public float EnergyPerStep = 12f;

    [DataField]
    public float SelfDamagePerStep = 1f;

    /// <summary>
    /// Выключается, если Integrity/MaxIntegrity &gt;= этот порог (критический износ).
    /// Integrity в Mech = накопленный урон.
    /// </summary>
    [DataField]
    public float MaxDamageRatio = 0.66f;

    [DataField]
    public EntProtoId ToggleAction = "ActionMechToggleOverload";

    [DataField]
    public EntityUid? ToggleActionEntity;
}
