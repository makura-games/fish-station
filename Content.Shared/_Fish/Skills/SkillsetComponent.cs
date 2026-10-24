using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Skills;

/// <summary>
/// Набор навыков сущности на текущий раунд.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSkillSystem), Other = AccessPermissions.Read)]
public sealed partial class SkillsetComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<SkillPrototype>, int> Skills = new();

    [DataField, AutoNetworkedField]
    public int RemainingPoints;

    [DataField, AutoNetworkedField]
    public int TotalPoints;

    [DataField, AutoNetworkedField]
    public ProtoId<JobPrototype>? JobId;

    [DataField, AutoNetworkedField]
    public bool BudgetGranted;

    [DataField, AutoNetworkedField]
    public bool Locked;

    [DataField]
    public Dictionary<ProtoId<SkillPrototype>, int> PendingAllocation = new();

    /// <summary>
    /// Уже пройденные учебные шаги (ключи SkillMath.MakeTrainingKey).
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<string> CompletedTrainings = new();
}
