using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    public async Task<List<FishAchievementProgress>> GetFishAchievementsAsync(
        Guid player,
        CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);
        return await db.DbContext.FishAchievementProgress
            .Where(entry => entry.PlayerUserId == player)
            .AsNoTracking()
            .ToListAsync(cancel);
    }

    /// <summary>
    /// Идемпотентно выставляет прогресс. Unlock фиксируется один раз.
    /// </summary>
    public async Task<FishAchievementProgress> UpsertFishAchievementProgressAsync(
        Guid player,
        string achievementId,
        int progress,
        int progressTarget,
        CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);
        var entry = await db.DbContext.FishAchievementProgress
            .Where(e => e.PlayerUserId == player && e.AchievementId == achievementId)
            .SingleOrDefaultAsync(cancel);

        var now = DateTimeOffset.UtcNow;
        progress = Math.Max(0, progress);
        var shouldUnlock = progressTarget > 0 && progress >= progressTarget;

        if (entry == null)
        {
            entry = new FishAchievementProgress
            {
                PlayerUserId = player,
                AchievementId = achievementId,
                Progress = progress,
                UnlockedAt = shouldUnlock ? now : null,
                UpdatedAt = now,
            };
            db.DbContext.FishAchievementProgress.Add(entry);
        }
        else
        {
            // Уже unlocked — идемпотентно, без лишних write/notify.
            if (entry.UnlockedAt != null)
                return entry;

            // Не уменьшаем уже накопленный прогресс и не сбрасываем unlock.
            if (progress > entry.Progress)
                entry.Progress = progress;

            if (entry.UnlockedAt == null && shouldUnlock)
                entry.UnlockedAt = now;

            entry.UpdatedAt = now;
        }

        await db.DbContext.SaveChangesAsync(cancel);
        return entry;
    }

    /// <summary>
    /// Удаляет весь прогресс достижений аккаунта (например, при перманентном бане).
    /// </summary>
    public async Task<int> DeleteFishAchievementsAsync(Guid player, CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);
        var rows = await db.DbContext.FishAchievementProgress
            .Where(e => e.PlayerUserId == player)
            .ToListAsync(cancel);

        if (rows.Count == 0)
            return 0;

        db.DbContext.FishAchievementProgress.RemoveRange(rows);
        await db.DbContext.SaveChangesAsync(cancel);
        return rows.Count;
    }
}
