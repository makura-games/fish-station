using Content.Shared._Fish.Skills;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry.EntitySystems;

public sealed partial class SolutionTransferSystem
{
    [Dependency] private SharedSkillSystem _skills = default!;

    private static readonly ProtoId<SkillPrototype> ChemistrySkill = "FishSkillChemistry";

    private TimeSpan GetChemistryTransferDelay(EntityUid user, TimeSpan baseTime)
        => baseTime * _skills.GetSkillDelayMultiplier(user, ChemistrySkill);
}
