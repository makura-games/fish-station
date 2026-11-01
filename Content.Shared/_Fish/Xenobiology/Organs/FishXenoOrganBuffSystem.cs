using Content.Shared.Movement.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Fish.Xenobiology.Organs;

/// <summary>
/// Движение и прочие shared-эффекты активного баффа ксено-органа.
/// </summary>
public sealed partial class FishXenoOrganBuffSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishXenoOrganBuffComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnRefreshSpeed(EntityUid uid, FishXenoOrganBuffComponent buff, RefreshMovementSpeedModifiersEvent args)
    {
        if (_timing.CurTime >= buff.EndsAt)
            return;

        if (buff.Kind == FishXenoOrganEffectKind.Vigor)
            args.ModifySpeed(1.25f);
    }
}
