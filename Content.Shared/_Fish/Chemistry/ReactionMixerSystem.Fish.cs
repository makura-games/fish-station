using Content.Shared._Fish.Skills;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry.Reaction;

public sealed partial class ReactionMixerSystem
{
    [Dependency] private SharedSkillSystem _skills = default!;

    private static readonly ProtoId<SkillPrototype> ChemistrySkill = "FishSkillChemistry";

    private TimeSpan GetChemistryMixDelay(EntityUid user, TimeSpan baseTime)
        => baseTime * _skills.GetSkillDelayMultiplier(user, ChemistrySkill);
}
