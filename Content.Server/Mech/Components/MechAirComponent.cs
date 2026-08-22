using Content.Server.Atmos;
using Content.Shared.Atmos;

namespace Content.Server.Mech.Components;

[RegisterComponent]
public sealed partial class MechAirComponent : Component
{
    /// <summary>
    /// Fish: cabin mix для airtight + MechCabinAtmos.UseInternalTank.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public GasMixture Air = new(GasMixVolume);

    public const float GasMixVolume = 70f;
}
