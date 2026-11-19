using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    public async Task<float?> GetFishRpuKarmaAsync(Guid player, CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);
        return await db.DbContext.FishRpuProfile
            .Where(entry => entry.PlayerUserId == player)
            .AsNoTracking()
            .Select(entry => (float?)entry.Karma)
            .FirstOrDefaultAsync(cancel);
    }

    /// <summary>
    /// Идемпотентно записывает карму игрока.
    /// </summary>
    public async Task SetFishRpuKarmaAsync(Guid player, float karma, CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);
        var entry = await db.DbContext.FishRpuProfile
            .Where(e => e.PlayerUserId == player)
            .SingleOrDefaultAsync(cancel);

        var now = DateTimeOffset.UtcNow;
        karma = Math.Clamp(karma, -100f, 100f);

        if (entry == null)
        {
            db.DbContext.FishRpuProfile.Add(new FishRpuProfile
            {
                PlayerUserId = player,
                Karma = karma,
                UpdatedAt = now,
            });
        }
        else
        {
            entry.Karma = karma;
            entry.UpdatedAt = now;
        }

        await db.DbContext.SaveChangesAsync(cancel);
    }
}
