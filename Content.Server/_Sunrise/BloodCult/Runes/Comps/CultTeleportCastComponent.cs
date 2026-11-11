namespace Content.Server._Sunrise.BloodCult.Runes.Comps;

/// <summary>
/// Временные данные каста личного телепорта культиста.
/// </summary>
[RegisterComponent]
public sealed partial class CultTeleportCastComponent : Component
{
    // Fish-start
    /// <summary>
    /// Целевая сущность для телепортации.
    /// </summary>
    public EntityUid Target;

    /// <summary>
    /// Выбранная руна назначения для телепортации.
    /// </summary>
    public EntityUid Rune;
    // Fish-end
}

