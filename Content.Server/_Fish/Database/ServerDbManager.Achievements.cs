using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Database;

public partial interface IServerDbManager
{
    Task<List<FishAchievementProgress>> GetFishAchievementsAsync(Guid player, CancellationToken cancel = default);

    Task<FishAchievementProgress> UpsertFishAchievementProgressAsync(
        Guid player,
        string achievementId,
        int progress,
        int progressTarget,
        CancellationToken cancel = default);

    Task<int> DeleteFishAchievementsAsync(Guid player, CancellationToken cancel = default);
}

public sealed partial class ServerDbManager
{
    public Task<List<FishAchievementProgress>> GetFishAchievementsAsync(Guid player, CancellationToken cancel = default)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetFishAchievementsAsync(player, cancel));
    }

    public Task<FishAchievementProgress> UpsertFishAchievementProgressAsync(
        Guid player,
        string achievementId,
        int progress,
        int progressTarget,
        CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() =>
            _db.UpsertFishAchievementProgressAsync(player, achievementId, progress, progressTarget, cancel));
    }

    public Task<int> DeleteFishAchievementsAsync(Guid player, CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.DeleteFishAchievementsAsync(player, cancel));
    }
}
