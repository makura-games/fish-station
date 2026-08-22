using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Mechs.Components;

/// <summary>
/// Медицинский sleeper: лечение + инъекция реагентов из бортового резервуара.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MechMedicalSleeperComponent : Component
{
    [DataField(required: true)]
    public Damage.DamageSpecifier HealPerSecond = new();

    /// <summary>
    /// Имя solution в SolutionContainerManager.
    /// </summary>
    [DataField]
    public string SolutionName = "sleeper";

    /// <summary>
    /// Объём одной дозы инъекции из бортового резервуара.
    /// </summary>
    [DataField]
    public FixedPoint2 InjectAmount = 7.5;

    [DataField]
    public float LoadDelay = 3f;

    [DataField]
    public float LoadEnergyDelta = -20f;
}
