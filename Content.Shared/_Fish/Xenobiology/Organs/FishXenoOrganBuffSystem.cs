using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
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
        SubscribeLocalEvent<FishXenoOrganBuffComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnRefreshSpeed(EntityUid uid, FishXenoOrganBuffComponent buff, RefreshMovementSpeedModifiersEvent args)
    {
        if (_timing.CurTime >= buff.EndsAt)
            return;

        if (buff.Kind == FishXenoOrganEffectKind.Vigor)
            args.ModifySpeed(1.25f);
    }

    private void OnDamageModify(EntityUid uid, FishXenoOrganBuffComponent buff, DamageModifyEvent args)
    {
        if (_timing.CurTime >= buff.EndsAt || buff.Kind != FishXenoOrganEffectKind.HeatResist)
            return;

        if (args.Damage.DamageDict.TryGetValue("Heat", out var heat) && heat > FixedPoint2.Zero)
            args.Damage.DamageDict["Heat"] = heat * 0.5f;
    }
}
