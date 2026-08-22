namespace Content.Shared._Fish.Achievements;

/// <summary>
/// Антиабуз-настройки достижения. Без gameplay-наград, только защита от фарма.
/// </summary>
public sealed partial class AchievementPrototype
{
    /// <summary>
    /// Не чаще одного прогресса за раунд, если у события нет EventKey.
    /// При наличии EventKey уникальность события важнее.
    /// </summary>
    [DataField]
    public bool OncePerRound = true;

    /// <summary>
    /// Минимальное время в раунде до прогресса (сек). 0 = без ограничения.
    /// </summary>
    [DataField]
    public float MinRoundSeconds;

    /// <summary>
    /// Минимальный интервал между тиками прогресса без EventKey (сек).
    /// </summary>
    [DataField]
    public float ProgressCooldownSeconds = 2f;

    /// <summary>
    /// Для kill/damage: только humanoid-жертвы с игроком.
    /// </summary>
    [DataField]
    public bool RequirePlayerVictim = true;

    /// <summary>
    /// Игнорировать самоубийства.
    /// </summary>
    [DataField]
    public bool IgnoreSuicide = true;

    /// <summary>
    /// Разрешить бинарный unlock без conditionParams.
    /// </summary>
    [DataField]
    public bool AllowGenericTrigger;

    /// <summary>
    /// Разрешить прогресс в Admin Test Arena (по умолчанию нет).
    /// </summary>
    [DataField]
    public bool AllowAdminArena;
}

/// <summary>
/// Контекст события для фильтрации и антиабуза.
/// </summary>
public readonly record struct AchievementTriggerContext(
    string? JobId = null,
    string? EventId = null,
    string? CounterKey = null,
    bool IsSuicide = false,
    bool VictimIsPlayerHumanoid = false,
    bool OnEmergencyShuttle = false,
    /// <summary>
    /// Уникальный ключ игрового события (kill:uid, heal:uid:bucket, …).
    /// Одно и то же EventKey не даёт повторный прогресс в раунде.
    /// </summary>
    string? EventKey = null,
    bool RequireInRound = true);
