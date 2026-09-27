using Content.Server.Administration.Systems;
using Content.Server.GameTicking;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._Fish.Achievements;
using Content.Shared.CCVar;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server._Fish.Achievements;

/// <summary>
/// Единый gate: gameplay (раунд / ghost / arena) и persistence (playtime → БД).
/// </summary>
public sealed partial class AchievementGameplayGateSystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private AdminTestArenaSystem _adminArena = default!;
    [Dependency] private PlayTimeTrackingManager _playTime = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    /// <summary>
    /// Можно ли засчитывать gameplay-достижение этому session.
    /// </summary>
    public bool CanEarnGameplay(
        ICommonSession session,
        AchievementPrototype? proto = null,
        bool requireInRound = true)
    {
        if (requireInRound && _ticker.RunLevel != GameRunLevel.InRound)
            return false;

        if (session.AttachedEntity is not { Valid: true } ent)
            return false;

        if (HasComp<GhostComponent>(ent))
            return false;

        if (!_mind.TryGetMind(ent, out _, out var mind) || mind.UserId != session.UserId)
            return false;

        if (mind.IsVisitingEntity)
            return false;

        if (proto is not { AllowAdminArena: true } && IsInAdminTestArena(ent))
            return false;

        return true;
    }

    /// <summary>
    /// Можно ли писать в fish_achievement_progress (anti-DB-flood для новорегов).
    /// </summary>
    public bool CanPersistToDatabase(ICommonSession session)
    {
        var minMinutes = _cfg.GetCVar(FishCVars.AchievementsMinOverallPlaytimeMinutes);
        if (minMinutes <= 0)
            return true;

        _playTime.FlushTracker(session);
        var overall = _playTime.GetPlayTimeForTracker(session, PlayTimeTrackingShared.TrackerOverall);
        return overall >= TimeSpan.FromMinutes(minMinutes);
    }

    public bool IsInAdminTestArena(EntityUid ent)
    {
        var mapUid = Transform(ent).MapUid;
        if (mapUid == null)
            return false;

        foreach (var arenaMap in _adminArena.ArenaMap.Values)
        {
            if (arenaMap == mapUid)
                return true;
        }

        if (TryComp(mapUid.Value, out MetaDataComponent? meta) &&
            meta.EntityName.StartsWith("ATAM-", StringComparison.Ordinal))
            return true;

        return false;
    }
}
