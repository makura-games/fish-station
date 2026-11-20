using Content.Shared._Fish.Skills;
using Robust.Shared.Prototypes;

namespace Content.Shared.RCD.Systems;

public sealed partial class RCDSystem
{
    private static readonly ProtoId<SkillPrototype> ConstructionSkill = "FishSkillConstruction";

    [Dependency] private SharedSkillSystem _skills = default!;

    /// <summary>
    /// РСУ-строительство требует базового Construction. true = отказ (popup уже показан).
    /// </summary>
    private bool TryDenyRcdConstruction(EntityUid user)
    {
        return _skills.TryDenyMissingSkill(user, ConstructionSkill, SkillLevels.Basic, "skills-rcd-need-construction");
    }

    private float GetRcdConstructionDelayMultiplier(EntityUid user)
    {
        return _skills.GetSkillDelayMultiplier(user, ConstructionSkill);
    }
}
