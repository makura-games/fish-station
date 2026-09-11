namespace Content.Server._Sunrise.BloodCult.Runes.Comps;

[RegisterComponent]
public sealed partial class SoulShardComponent : Component
{
    /// <summary>
    /// Был ли в осколке занятый разум — нужно для авто-включения ghost role после ghosting.
    /// </summary>
    [DataField]
    public bool HadSoul;

    /// <summary>
    /// Автоматически открывать ghost role, когда разум покидает осколок.
    /// </summary>
    [DataField]
    public bool AutoGhostRoleOnMindRemoved = true;
}
