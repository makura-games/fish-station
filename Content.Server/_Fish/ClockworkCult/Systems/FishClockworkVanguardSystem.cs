using Content.Shared._Fish.ClockworkCult.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Fish.ClockworkCult.Systems;

/// <summary>
/// Vanguard: поглощение stun/knockdown и отдача 25% (или двойной бессознательный при >30с).
/// </summary>
public sealed partial class FishClockworkVanguardSystem : EntitySystem
{
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly EntProtoId StunEffect = "StatusEffectStunned";
    private static readonly EntProtoId KnockdownEffect = "StatusEffectKnockdown";

    public override void Initialize()
    {
        base.Initialize();

        // Нельзя дублировать подписку на StatusEffectAppliedEvent (занята SharedStunSystem).
        SubscribeLocalEvent<FishClockworkVanguardComponent, BeforeStatusEffectAddedEvent>(OnBeforeStatusEffect);
        SubscribeLocalEvent<FishClockworkVanguardComponent, KnockDownAttemptEvent>(OnKnockDownAttempt);
        SubscribeLocalEvent<FishClockworkVanguardComponent, StunnedEvent>(OnStunned);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FishClockworkVanguardComponent>();
        while (query.MoveNext(out var uid, out var vanguard))
        {
            if (vanguard.EndsAt > _timing.CurTime)
                continue;

            ExpireVanguard(uid, vanguard);
        }
    }

    private void OnBeforeStatusEffect(Entity<FishClockworkVanguardComponent> ent, ref BeforeStatusEffectAddedEvent args)
    {
        // StatusEffectKnockdown без KnockDownAttempt — отменяем сразу с оценкой длительности.
        if (args.Effect != KnockdownEffect)
            return;

        args.Cancelled = true;
        Absorb(ent, 2f);
    }

    private void OnKnockDownAttempt(Entity<FishClockworkVanguardComponent> ent, ref KnockDownAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancelled = true;
        Absorb(ent, (float)(args.Time?.TotalSeconds ?? 1.0));
    }

    private void OnStunned(Entity<FishClockworkVanguardComponent> ent, ref StunnedEvent args)
    {
        // Stun уже применён SharedStunSystem — снимаем и учитываем оставшееся время.
        AbsorbAndStrip(ent.Owner, ent.Comp, StunEffect);
    }

    private void AbsorbAndStrip(EntityUid target, FishClockworkVanguardComponent vanguard, EntProtoId effect)
    {
        var seconds = 3f;
        if (_status.TryGetTime(target, effect, out var time) && time.EndEffectTime != null)
            seconds = MathF.Max(0.5f, (float)(time.EndEffectTime.Value - _timing.CurTime).TotalSeconds);

        Absorb((target, vanguard), seconds);
        _status.TryRemoveStatusEffect(target, effect);
    }

    private void Absorb(Entity<FishClockworkVanguardComponent> ent, float seconds)
    {
        ent.Comp.AbsorbedStunSeconds += MathF.Max(0f, seconds);
        Dirty(ent);
    }

    private void ExpireVanguard(EntityUid uid, FishClockworkVanguardComponent vanguard)
    {
        var absorbed = vanguard.AbsorbedStunSeconds;
        // Сразу, до возврата паралича: иначе OnStunned снова поглотит stun.
        RemComp<FishClockworkVanguardComponent>(uid);

        if (absorbed <= 0.1f)
            return;

        // tg: >30с поглощения → бессознательность на удвоенное время; иначе 25% станов.
        if (absorbed > 30f)
            _stun.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(absorbed * 2f));
        else
            _stun.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(absorbed * 0.25f));
    }
}
