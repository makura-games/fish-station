using Content.Shared._Fish.Skills;
using Robust.Shared.Prototypes;

namespace Content.Shared.Burial;

public sealed partial class BurialSystem
{
    [Dependency] private SharedSkillSystem _skills = default!;

    private static readonly ProtoId<SkillPrototype> AthleticsSkill = "FishSkillAthletics";

    private float GetAthleticsDigDelayMultiplier(EntityUid user)
        => _skills.GetSkillDelayMultiplier(user, AthleticsSkill);
}
