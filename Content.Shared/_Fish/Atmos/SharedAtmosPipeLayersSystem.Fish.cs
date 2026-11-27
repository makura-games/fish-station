using Content.Shared._Fish.Skills;
using Robust.Shared.Prototypes;

namespace Content.Shared.Atmos.EntitySystems;

public abstract partial class SharedAtmosPipeLayersSystem
{
    [Dependency] private SharedSkillSystem _skills = default!;

    private static readonly ProtoId<SkillPrototype> AtmosSkill = "FishSkillAtmos";

    private float GetAtmosPipeDelay(EntityUid user, float baseDelay)
        => baseDelay * _skills.GetSkillDelayMultiplier(user, AtmosSkill);
}
