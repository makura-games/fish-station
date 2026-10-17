namespace Content.Server._Fish.Mech;

/// <summary>
/// Stores a brain's original restrictions and components temporarily added while piloting a mech.
/// </summary>
[RegisterComponent]
public sealed partial class MechBrainCombatComponent : Component
{
    /// <summary>
    /// The brain's original interaction restriction.
    /// </summary>
    public bool BlockInteraction;

    /// <summary>
    /// Whether piloting added a BlockMovementComponent that must be removed on exit.
    /// </summary>
    public bool AddedBlockMovement;

    /// <summary>
    /// Whether piloting added a CombatModeComponent that must be removed on exit.
    /// </summary>
    public bool AddedCombatMode;

    /// <summary>
    /// The combat mode to restore when piloting ends.
    /// </summary>
    public bool InitialCombatMode;
}
