using System;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Database;

public partial interface IServerDbManager
{
    Task<float?> GetFishRpuKarmaAsync(Guid player, CancellationToken cancel = default);

    Task SetFishRpuKarmaAsync(Guid player, float karma, CancellationToken cancel = default);
}

public sealed partial class ServerDbManager
{
    public Task<float?> GetFishRpuKarmaAsync(Guid player, CancellationToken cancel = default)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetFishRpuKarmaAsync(player, cancel));
    }

    public Task SetFishRpuKarmaAsync(Guid player, float karma, CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.SetFishRpuKarmaAsync(player, karma, cancel));
    }
}
