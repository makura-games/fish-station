using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Rpu;

/// <summary>
/// Имена служебных прототипов системы.
/// </summary>
public static class RpuPrototypeIds
{
    public static readonly ProtoId<RpuTuningPrototype> Tuning = "RpuDefault";

    public static readonly ProtoId<RpuViolationPrototype> KillCrew = "RpuKillCrew";
    public static readonly ProtoId<RpuViolationPrototype> KillSecurityChain = "RpuKillSecurityChain";
    public static readonly ProtoId<RpuViolationPrototype> Crit = "RpuCrit";
    public static readonly ProtoId<RpuViolationPrototype> ExplosionSmall = "RpuExplosionSmall";
    public static readonly ProtoId<RpuViolationPrototype> ExplosionMedium = "RpuExplosionMedium";
    public static readonly ProtoId<RpuViolationPrototype> ExplosionLarge = "RpuExplosionLarge";
    public static readonly ProtoId<RpuViolationPrototype> AirlockBasic = "RpuAirlockBasic";
    public static readonly ProtoId<RpuViolationPrototype> AirlockService = "RpuAirlockService";
    public static readonly ProtoId<RpuViolationPrototype> AirlockDepartment = "RpuAirlockDepartment";
    public static readonly ProtoId<RpuViolationPrototype> AirlockHighSecurity = "RpuAirlockHighSecurity";
    public static readonly ProtoId<RpuViolationPrototype> WireCut = "RpuWireCut";
    public static readonly ProtoId<RpuViolationPrototype> StructureDestroyed = "RpuStructureDestroyed";
    public static readonly ProtoId<RpuViolationPrototype> StructureDeconstructed = "RpuStructureDeconstructed";
    public static readonly ProtoId<RpuViolationPrototype> SabotageAtmos = "RpuSabotageAtmos";
    public static readonly ProtoId<RpuViolationPrototype> BreachDepressurize = "RpuBreachDepressurize";
    public static readonly ProtoId<RpuViolationPrototype> SpillHazard = "RpuSpillHazard";
    public static readonly ProtoId<RpuViolationPrototype> SlipCrew = "RpuSlipCrew";
    public static readonly ProtoId<RpuViolationPrototype> SlipVip = "RpuSlipVip";
    public static readonly ProtoId<RpuViolationPrototype> SingularityUnattended = "RpuSingularityUnattended";
    public static readonly ProtoId<RpuViolationPrototype> SingularityEscaped = "RpuSingularityEscaped";
    public static readonly ProtoId<RpuViolationPrototype> ContrabandItem = "RpuContrabandItem";
}
