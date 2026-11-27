using Content.Shared._Fish.Skills;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Medical.Healing;

public sealed partial class HealingSystem
{
    [Dependency] private SharedSkillSystem _skills = default!;
}
