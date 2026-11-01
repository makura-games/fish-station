using Content.Shared._Fish.Xenobiology.Organs;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Xenobiology.Organs;

/// <summary>
/// Первая порция эффектов ксено-органов: временный бафф после использования на себе.
/// </summary>
public sealed partial class FishXenoOrganEffectSystem : EntitySystem
{
    private static readonly TimeSpan BuffDuration = TimeSpan.FromSeconds(60);

    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishXenoOrganComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<FishXenoOrganComponent> organ, ref UseInHandEvent args)
    {
        if (args.Handled || !organ.Comp.Identified)
            return;

        if (!TryComp<FishXenoOrganEffectComponent>(organ, out var effect))
            return;

        args.Handled = true;
        ApplyBuff(args.User, effect.Kind);
        _popup.PopupEntity(Loc.GetString("fish-xeno-organ-used"), args.User, args.User);
        QueueDel(organ);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FishXenoOrganBuffComponent>();
        while (query.MoveNext(out var uid, out var buff))
        {
            if (_timing.CurTime < buff.EndsAt)
                continue;

            RemComp<FishXenoOrganBuffComponent>(uid);
        }
    }

    private void ApplyBuff(EntityUid target, FishXenoOrganEffectKind kind)
    {
        var buff = EnsureComp<FishXenoOrganBuffComponent>(target);
        buff.Kind = kind;
        buff.EndsAt = _timing.CurTime + BuffDuration;
        Dirty(target, buff);

        switch (kind)
        {
            case FishXenoOrganEffectKind.Regenerative:
                _damage.TryChangeDamage(target, Heal("Blunt", 4));
                break;
            case FishXenoOrganEffectKind.Toxic:
                if (_solutions.TryGetInjectableSolution(target, out var soln, out _))
                    _solutions.TryAddReagent(soln, "Toxin", FixedPoint2.New(5));
                break;
            case FishXenoOrganEffectKind.Sinew:
                _damage.TryChangeDamage(target, Heal("Slash", 4));
                break;
        }
    }

    private static DamageSpecifier Heal(string type, float amount)
    {
        var spec = new DamageSpecifier();
        spec.DamageDict[type] = FixedPoint2.New(-amount);
        return spec;
    }
}
