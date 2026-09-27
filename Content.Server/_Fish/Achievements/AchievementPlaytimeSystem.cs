using Content.Server.Players.PlayTimeTracking;
using Content.Shared._Fish.Achievements;
using Content.Shared.CCVar;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Shared.Configuration;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Achievements;

/// <summary>
/// Account-wide playtime thresholds → achievement unlock.
/// </summary>
public sealed partial class AchievementPlaytimeSystem : EntitySystem
{
    [Dependency] private AchievementManager _achievements = default!;
    [Dependency] private PlayTimeTrackingManager _playTime = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
        base.Shutdown();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + TimeSpan.FromMinutes(1);

        foreach (var session in _players.Sessions)
        {
            if (session.Status != SessionStatus.Connected)
                continue;

            CheckSession(session);
        }
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Connected)
            CheckSession(args.Session);
    }

    private void CheckSession(ICommonSession session)
    {
        if (!_achievements.TryGetState(session, out _))
            return;

        _playTime.FlushTracker(session);
        var overall = _playTime.GetPlayTimeForTracker(session, PlayTimeTrackingShared.TrackerOverall);
        var minutes = (int)overall.TotalMinutes;

        _ = _achievements.ContributeAsync(
            session,
            AchievementConditionKeys.PlaytimeMinutes,
            new AchievementTriggerContext(
                PlaytimeMinutes: minutes,
                EventKey: $"playtime:{session.UserId}:{minutes / 60}h",
                RequireInRound: false));
    }
}
