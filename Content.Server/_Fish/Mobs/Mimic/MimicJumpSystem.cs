using Content.Shared._Fish.Mobs.Mimic;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;

namespace Content.Server._Fish.Mobs.Mimic;

/// <summary>
/// Прыжок мимика: моб отбрасывает сам себя в сторону выбранной точки.
/// Механика полностью самостоятельная чтото типо дубликата прыжка зомби но с своей темой
/// </summary>
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
    }

    private void OnJump(EntityUid uid, MimicJumpComponent component, MimicJumpActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var mapCoords = _transform.ToMapCoordinates(args.Target);
        var position = _transform.GetMapCoordinates(uid).Position;
        var direction = mapCoords.Position - position;

        // Дальность прыжка ограничена, чтобы мимик не улетал через весь контур.
        if (direction.Length() > component.MaxJumpDistance)
            direction = direction.Normalized() * component.MaxJumpDistance;

        _throwing.TryThrow(uid, direction, component.JumpSpeed, uid, component.PushbackRatio);
    }

    /// <summary>
    /// Цель в которую влетел мимик падает в стан
    /// </summary>
    private void OnThrowDoHit(EntityUid uid, MimicJumpComponent component, ThrowDoHitEvent args)
    {
        if (_mobState.IsDead(uid))
            return;

        // Мимики друг друга не оглушают.
        if (HasComp<MimicJumpComponent>(args.Target))
            return;

        if (!_mobState.IsAlive(args.Target))
            return;

        _stun.TryAddParalyzeDuration(args.Target, TimeSpan.FromSeconds(component.ParalyzeTime));
        _damageable.TryChangeDamage(args.Target, component.Damage, origin: args.Thrown);
    }
}
