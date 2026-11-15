// ReSharper disable once CheckNamespace
namespace Content.Shared._Sunrise.Abilities.Milira;

public sealed partial class WingFlightComponent
{
    [DataField]
    public bool UsesStamina = true;

    [DataField]
    public bool ClearLowObstacles;

    [DataField]
    public bool UpdateWingMarkings = true;
}
