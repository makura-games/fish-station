using Content.Shared.Actions;
using Content.Shared._Sunrise.Laws;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.SecurityCrimeReport;

/// <summary>
/// InstantAction: open the crime-report radial (client). Does not send a message by itself.
/// </summary>
public sealed partial class OpenSecurityCrimeReportEvent : InstantActionEvent;

/// <summary>
/// Client → server: officer picked an article from the radial menu.
/// </summary>
[Serializable, NetSerializable]
public sealed class SecurityCrimeReportSelectedEvent : EntityEventArgs
{
    public NetEntity Device;
    public ProtoId<CorporateLawPrototype> Law;

    public SecurityCrimeReportSelectedEvent(NetEntity device, ProtoId<CorporateLawPrototype> law)
    {
        Device = device;
        Law = law;
    }
}
