using Content.Shared._Fish.Xenobiology.Extracts;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Xenobiology.Extracts;

/// <summary>
/// Остывание зелья sentience: −1 стадия разогрева каждые 60 с (упрощённый SS220).
/// </summary>
public sealed partial class FishSlimeSentiencePotionCoolSystem : EntitySystem
{
    private static readonly TimeSpan CoolInterval = TimeSpan.FromSeconds(60);

    [Dependency] private IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FishSlimeSentiencePotionComponent>();
        while (query.MoveNext(out var uid, out var warm))
        {
            if (warm.WarmStage <= 0)
                continue;

            if (warm.LastWarmChange == TimeSpan.Zero)
            {
                warm.LastWarmChange = _timing.CurTime;
                Dirty(uid, warm);
                continue;
            }

            if (_timing.CurTime < warm.LastWarmChange + CoolInterval)
                continue;

            warm.WarmStage--;
            warm.LastWarmChange = _timing.CurTime;
            Dirty(uid, warm);
        }
    }
}
