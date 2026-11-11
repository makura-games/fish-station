using System;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Shared.Database;
using Robust.Server.Player;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Fish.Achievements;

/// <summary>
/// При перманентном server-ban удаляет fish_achievement_progress и RAM-кеш аккаунта.
/// </summary>
public sealed partial class AchievementBanCleanupSystem : EntitySystem
{
    [Dependency] private IBanManager _bans = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private AchievementManager _achievements = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private ILogManager _log = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _log.GetSawmill("fish.achievements");
        _bans.ServerBanIssued += OnServerBanIssued;
    }

    public override void Shutdown()
    {
        _bans.ServerBanIssued -= OnServerBanIssued;
        base.Shutdown();
    }

    private void OnServerBanIssued(object? sender, ServerBanIssuedEvent ev)
    {
        if (ev.BanDef is not { Type: BanType.Server, ExpirationTime: null } ban)
            return;

        if (ban.UserIds.Length == 0)
            return;

        foreach (var userId in ban.UserIds)
            _ = PurgePlayerAsync(userId);
    }

    private async Task PurgePlayerAsync(NetUserId userId)
    {
        try
        {
            var deleted = await _db.DeleteFishAchievementsAsync(userId.UserId);
            _players.TryGetSessionById(userId, out var session);
            _achievements.PurgePlayer(userId, session);

            if (deleted > 0)
            {
                _sawmill.Info(
                    "Removed {Count} achievement rows for permanently banned user {User}",
                    deleted,
                    userId);
            }
        }
        catch (Exception ex)
        {
            _sawmill.Error("Failed to purge achievements for permanently banned user {User}:\n{Exception}", userId, ex);
        }
    }
}
