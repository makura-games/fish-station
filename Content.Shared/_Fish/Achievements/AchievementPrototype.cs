using System.Collections.Generic;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Fish.Achievements;

/// <summary>
/// Декларативное определение достижения. Без наград и gameplay-бонусов.
/// </summary>
[Prototype]
public sealed partial class AchievementPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// LocId названия.
    /// </summary>
    [DataField(required: true)]
    public LocId Name = string.Empty;

    /// <summary>
    /// LocId описания / условия для UI.
    /// </summary>
    [DataField(required: true)]
    public LocId Description = string.Empty;

    /// <summary>
    /// LocId текста для секретных достижений до unlock.
    /// </summary>
    [DataField]
    public LocId? SecretDescription;

    [DataField(required: true)]
    public ProtoId<AchievementCategoryPrototype> Category;

    /// <summary>
    /// Ключ семейства условий (см. <see cref="AchievementConditionKeys"/>).
    /// </summary>
    [DataField(required: true)]
    public string Condition = string.Empty;

    /// <summary>
    /// Дополнительные параметры условия (прототипы, роли, пороги и т.п.).
    /// </summary>
    [DataField]
    public Dictionary<string, string> ConditionParams = new();

    /// <summary>
    /// Целевой прогресс. 1 = бинарное достижение.
    /// </summary>
    [DataField]
    public int ProgressTarget = 1;

    /// <summary>
    /// Скрывать полное описание до получения.
    /// </summary>
    [DataField]
    public bool Secret;

    /// <summary>
    /// Необязательная иконка (разрешённый RSI/path).
    /// </summary>
    [DataField]
    public SpriteSpecifier? Icon;

    /// <summary>
    /// Порядок внутри категории.
    /// </summary>
    [DataField]
    public int Order;
}

/// <summary>
/// Состояние достижения игрока для сети и UI.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct AchievementPlayerState(
    string AchievementId,
    int Progress,
    bool Unlocked,
    TimeSpan? UnlockedAt);
