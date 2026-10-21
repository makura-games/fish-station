using Content.Shared._Fish.Mobs.Mimic;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;

namespace Content.Server._Fish.Mobs.Mimic;

public sealed partial class MimicJumpSystem : EntitySystem
{
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MimicJumpComponent, MimicJumpActionEvent>(OnJump);
        SubscribeLocalEvent<MimicJumpComponent, ThrowDoHitEvent>(OnThrowDoHit);
        SubscribeLocalEvent<MimicJumpComponent, StopThrowEvent>(OnStopThrow);
    }

    private void OnJump(Entity<MimicJumpComponent> ent, ref MimicJumpActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var mapCoords = _transform.ToMapCoordinates(args.Target);
        var position = _transform.GetMapCoordinates(ent).Position;
        var direction = mapCoords.Position - position;

        if (direction.Length() > ent.Comp.MaxJumpDistance)
            direction = direction.Normalized() * ent.Comp.MaxJumpDistance;

        _throwing.TryThrow(ent, direction, ent.Comp.JumpSpeed, ent, ent.Comp.PushbackRatio);

        ent.Comp.Jumping = HasComp<ThrownItemComponent>(ent);
    }

    private void OnThrowDoHit(Entity<MimicJumpComponent> ent, ref ThrowDoHitEvent args)
    {
        if (!ent.Comp.Jumping)
            return;

        if (_mobState.IsDead(ent))
            return;

        if (HasComp<MimicJumpComponent>(args.Target))
            return;

        if (!_mobState.IsAlive(args.Target))
            return;

        _stun.TryAddParalyzeDuration(args.Target, TimeSpan.FromSeconds(ent.Comp.ParalyzeTime));
        _damageable.TryChangeDamage(args.Target, ent.Comp.Damage, origin: args.Thrown);
    }

    private void OnStopThrow(Entity<MimicJumpComponent> ent, ref StopThrowEvent args)
    {
        ent.Comp.Jumping = false;
    }
}
