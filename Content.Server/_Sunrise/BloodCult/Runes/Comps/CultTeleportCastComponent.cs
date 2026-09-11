namespace Content.Server._Sunrise.BloodCult.Runes.Comps;

/// <summary>
/// Временные данные каста личного телепорта культиста.
/// </summary>
[RegisterComponent]
public sealed partial class CultTeleportCastComponent : Component
{
    [DataField]
    public EntityUid Target;
}
