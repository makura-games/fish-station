using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Achievements;

/// <summary>
/// Клиент запрашивает собственный снимок прогресса.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestAchievementsEvent : EntityEventArgs;

/// <summary>
/// Полный снимок прогресса игрока (только unlock/progress строки).
/// Определения берутся из локальных прототипов.
/// </summary>
[Serializable, NetSerializable]
public sealed class AchievementsSnapshotEvent : EntityEventArgs
{
    public List<AchievementPlayerState> Entries;

    public AchievementsSnapshotEvent(List<AchievementPlayerState> entries)
    {
        Entries = entries;
    }
}

/// <summary>
/// Дельта после unlock или изменения прогресса.
/// </summary>
[Serializable, NetSerializable]
public sealed class AchievementProgressUpdatedEvent : EntityEventArgs
{
    public AchievementPlayerState Entry;
    public bool JustUnlocked;
    public string? NotificationLocId;

    public AchievementProgressUpdatedEvent(
        AchievementPlayerState entry,
        bool justUnlocked,
        string? notificationLocId = null)
    {
        Entry = entry;
        JustUnlocked = justUnlocked;
        NotificationLocId = notificationLocId;
    }
}
