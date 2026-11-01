using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Skills;

/// <summary>
/// Бюджет очков и min/max навыков для роли.
/// </summary>
[Prototype]
public sealed partial class JobSkillBudgetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public ProtoId<JobPrototype>? Job;

    /// <summary>
    /// Свободные очки сверх минимумов роли.
    /// </summary>
    [DataField]
    public int SkillPoints = 14;

    [DataField]
    public Dictionary<ProtoId<SkillPrototype>, int> MinSkills = new();

    [DataField]
    public Dictionary<ProtoId<SkillPrototype>, int> MaxSkills = new();

    public ProtoId<JobPrototype> GetJobId() => Job ?? ID;
}
