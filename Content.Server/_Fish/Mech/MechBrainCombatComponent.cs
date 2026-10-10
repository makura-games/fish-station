namespace Content.Server._Fish.Mech;

/// <summary>
/// Хранит исходные ограничения мозга и компоненты, выданные на время пилотирования меха.
/// </summary>
[RegisterComponent]
public sealed partial class MechBrainCombatComponent : Component
{
    /// <summary>
    /// Исходное состояние запрета взаимодействий мозга.
    /// </summary>
    public bool BlockInteraction;

    /// <summary>
    /// Был ли BlockMovementComponent добавлен этой системой.
    /// </summary>
    public bool AddedBlockMovement;

    /// <summary>
    /// Был ли CombatModeComponent добавлен этой системой.
    /// </summary>
    public bool AddedCombatMode;

    /// <summary>
    /// Исходное состояние боевого режима мозга.
    /// </summary>
    public bool InitialCombatMode;
}
