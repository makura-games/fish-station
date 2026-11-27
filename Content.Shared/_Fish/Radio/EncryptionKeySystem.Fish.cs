using Content.Shared._Fish.Skills;
using Robust.Shared.Prototypes;

namespace Content.Shared.Radio.EntitySystems;

public sealed partial class EncryptionKeySystem
{
    [Dependency] private SharedSkillSystem _skills = default!;

    private static readonly ProtoId<SkillPrototype> ComputerSkill = "FishSkillComputer";

    private float GetComputerKeyExtractDelay(EntityUid user, float baseDelay)
        => baseDelay * _skills.GetSkillDelayMultiplier(user, ComputerSkill);
}
