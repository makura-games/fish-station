#pragma warning disable IDE0130
using Content.Shared.Access;
using Robust.Shared.Prototypes;

namespace Content.Shared.AlertLevel;

public sealed partial class AlertLevelPrototype
{
    [DataField]
    public bool ForceEndRound;

    [DataField]
    public ExtendedAccessOptions? ExtendedAccessOptions;
}

[DataDefinition]
public partial record struct ExtendedAccessOptions
{
    [DataField]
    public LocId? Announcement;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Access group globally granted to readers participating in alert-level access changes.
    /// </summary>
    [DataField]
    public ProtoId<AccessGroupPrototype>? AccessGroup;
}
