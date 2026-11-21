using Content.Shared._Fish.Skills;
using Robust.Shared.Prototypes;

namespace Content.Shared.Prying.Systems;

public sealed partial class PryingSystem
{
    [Dependency] private SharedSkillSystem _skills = default!;

    private static readonly ProtoId<SkillPrototype> AthleticsSkill = "FishSkillAthletics";

    private float GetAthleticsPryDelayMultiplier(EntityUid user)
        => _skills.GetSkillDelayMultiplier(user, AthleticsSkill);
}
