using Robust.Shared.Prototypes;

namespace Content.Shared.Mech.Components;

public sealed partial class MechComponent
{
    /// <summary>
    /// Optional mass scanner action granted to the current pilot.
    /// </summary>
    [DataField]
    public EntProtoId? MechMassScannerAction;

    /// <summary>
    /// Runtime entity of the mass scanner action provided by this mech.
    /// </summary>
    public EntityUid? MechMassScannerActionEntity;
}
