using Content.Shared.Explosion;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.DiskLeaveNuke;

[RegisterComponent]
public sealed partial class DiskLeaveNukeComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromMinutes(5);

    [DataField]
    public TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    [DataField]
    public ProtoId<RadioChannelPrototype> RadioChannel = "CentCom";

    [DataField]
    public LocId WarningMessage = "fish-disk-leave-warning";

    [DataField]
    public LocId StrikeMessage = "fish-disk-leave-strike";

    [DataField]
    public LocId WarningNoHolder = "fish-disk-leave-warning-no-holder";

    [DataField]
    public LocId StrikeNoHolder = "fish-disk-leave-strike-no-holder";

    [DataField]
    public ProtoId<ExplosionPrototype> ExplosionType = "BluespaceArtilleryHE";

    [DataField]
    public float TotalIntensity = 2094f;

    [DataField]
    public float Slope = 16f;

    [DataField]
    public float MaxIntensity = 80f;

    [DataField]
    public float TileBreakScale = 1.5f;

    [DataField]
    public bool CanCreateVacuum = true;

    public TimeSpan NextCheck;

    public TimeSpan? StrikeTime;
}
