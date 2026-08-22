using System.Collections.Generic;
using Content.Shared._Fish.Achievements;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Client._Fish.Achievements;

/// <summary>
/// Клиентская синхронизация состояния достижений. Unlock API отсутствует намеренно.
/// </summary>
public sealed class AchievementClientSystem : EntitySystem
{
    public event Action<List<AchievementPlayerState>>? SnapshotReceived;
    public event Action<AchievementProgressUpdatedEvent>? ProgressReceived;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<AchievementsSnapshotEvent>(OnSnapshot);
        SubscribeNetworkEvent<AchievementProgressUpdatedEvent>(OnProgress);
    }

    public void RequestSnapshot()
    {
        RaiseNetworkEvent(new RequestAchievementsEvent());
    }

    private void OnSnapshot(AchievementsSnapshotEvent ev, EntitySessionEventArgs args)
    {
        SnapshotReceived?.Invoke(ev.Entries);
    }

    private void OnProgress(AchievementProgressUpdatedEvent ev, EntitySessionEventArgs args)
    {
        ProgressReceived?.Invoke(ev);
    }
}
