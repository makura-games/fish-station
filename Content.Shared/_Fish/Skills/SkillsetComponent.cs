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
    /// <summary>
    /// Текущие уровни навыков. Ключ — идентификатор <see cref="SkillPrototype"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<SkillPrototype>, int> Skills = new();

    /// <summary>
    /// Непотраченные очки сверх минимумов роли.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int RemainingPoints;

    /// <summary>
    /// Полный бюджет очков сверх минимумов роли.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int TotalPoints;

    /// <summary>
    /// Роль, по которой выданы бюджет и минимумы навыков.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<JobPrototype>? JobId;

    /// <summary>
    /// Бюджет роли уже выдан этой сущности.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool BudgetGranted;

    /// <summary>
    /// Распределение подтверждено. Пока флаг включён, менять уровни нельзя.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Locked;

    /// <summary>
    /// Уже пройденные учебные шаги (ключи SkillMath.MakeTrainingKey).
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<string> CompletedTrainings = new();
}
