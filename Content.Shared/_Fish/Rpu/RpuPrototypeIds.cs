using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Rpu;

/// <summary>
/// Имена служебных прототипов системы.
/// </summary>
public static class RpuPrototypeIds
{
    public static readonly ProtoId<RpuTuningPrototype> Tuning = "FishRpuDefault";

    public static readonly ProtoId<RpuViolationPrototype> KillCrew = "FishRpuKillCrew";
    public static readonly ProtoId<RpuViolationPrototype> KillSecurityChain = "FishRpuKillSecurityChain";
    public static readonly ProtoId<RpuViolationPrototype> Crit = "FishRpuCrit";
    public static readonly ProtoId<RpuViolationPrototype> ExplosionSmall = "FishRpuExplosionSmall";
    public static readonly ProtoId<RpuViolationPrototype> ExplosionMedium = "FishRpuExplosionMedium";
    public static readonly ProtoId<RpuViolationPrototype> ExplosionLarge = "FishRpuExplosionLarge";
    public static readonly ProtoId<RpuViolationPrototype> AirlockBasic = "FishRpuAirlockBasic";
    public static readonly ProtoId<RpuViolationPrototype> AirlockService = "FishRpuAirlockService";
    public static readonly ProtoId<RpuViolationPrototype> AirlockDepartment = "FishRpuAirlockDepartment";
    public static readonly ProtoId<RpuViolationPrototype> AirlockHighSecurity = "FishRpuAirlockHighSecurity";
    public static readonly ProtoId<RpuViolationPrototype> WireCut = "FishRpuWireCut";
    public static readonly ProtoId<RpuViolationPrototype> StructureDestroyed = "FishRpuStructureDestroyed";
    public static readonly ProtoId<RpuViolationPrototype> StructureDeconstructed = "FishRpuStructureDeconstructed";
    public static readonly ProtoId<RpuViolationPrototype> SabotageAtmos = "FishRpuSabotageAtmos";
    public static readonly ProtoId<RpuViolationPrototype> BreachDepressurize = "FishRpuBreachDepressurize";
    public static readonly ProtoId<RpuViolationPrototype> SpillHazard = "FishRpuSpillHazard";
    public static readonly ProtoId<RpuViolationPrototype> SlipCrew = "FishRpuSlipCrew";
    public static readonly ProtoId<RpuViolationPrototype> SlipVip = "FishRpuSlipVip";
    public static readonly ProtoId<RpuViolationPrototype> SingularityUnattended = "FishRpuSingularityUnattended";
    public static readonly ProtoId<RpuViolationPrototype> SingularityEscaped = "FishRpuSingularityEscaped";
    public static readonly ProtoId<RpuViolationPrototype> ContrabandItem = "FishRpuContrabandItem";
}
