using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Shared._Fish.Achievements;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Achievements;

/// <summary>
/// Сеть и server-side hooks семейств условий.
/// </summary>
public sealed partial class AchievementSystem : EntitySystem
{
    [Dependency] private AchievementManager _achievements = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan SnapshotRequestCooldown = TimeSpan.FromSeconds(2);
    private readonly Dictionary<NetUserId, TimeSpan> _lastSnapshotRequest = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RequestAchievementsEvent>(OnRequestAchievements);
        _achievements.ProgressChanged += OnProgressChanged;
    }

    public override void Shutdown()
    {
        _achievements.ProgressChanged -= OnProgressChanged;
        base.Shutdown();
    }

    private void OnRequestAchievements(RequestAchievementsEvent ev, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.UserId;
        var now = _timing.CurTime;
        if (_lastSnapshotRequest.TryGetValue(user, out var last) && now - last < SnapshotRequestCooldown)
            return;

        _lastSnapshotRequest[user] = now;
        SendSnapshot(args.SenderSession);
    }

    private void OnProgressChanged(ICommonSession session, AchievementPlayerState state, bool justUnlocked)
    {
        string? notif = null;
        if (justUnlocked)
            notif = "fish-achievements-unlocked";

        RaiseNetworkEvent(new AchievementProgressUpdatedEvent(state, justUnlocked, notif), session);
    }

    public void SendSnapshot(ICommonSession session)
    {
        var snapshot = _achievements.GetSnapshot(session);
        RaiseNetworkEvent(new AchievementsSnapshotEvent(snapshot), session);
    }

    /// <summary>
    /// Публичный API для condition handlers.
    /// </summary>
    public Task<bool> TryUnlockAsync(ICommonSession session, string achievementId)
    {
        return _achievements.TryUnlockAsync(session, achievementId);
    }

    public Task<bool> TryAddProgressAsync(ICommonSession session, string achievementId, int delta = 1)
    {
        return _achievements.TryAddProgressAsync(session, achievementId, delta);
    }
}
