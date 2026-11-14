using Content.Server.Chat.Systems;
using Content.Shared._Fish.Ipc;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Ipc;

/// <summary>
/// Жужжание только в момент урона, с кулдауном.
/// В крите вместо цикла — одна случайная эмоция раз в 30–50 секунд.
/// </summary>
public sealed partial class FishEmitBuzzWhileDamagedSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishEmitBuzzWhileDamagedComponent, DamageChangedEvent>(OnDamage);
    }

    private void OnDamage(Entity<FishEmitBuzzWhileDamagedComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null || _mobState.IsDead(ent))
            return;

        if (TryComp<FishIpcComponent>(ent.Owner, out var ipc) && ipc.SuppressDamageBuzz)
            return;

        if (PositiveDamage(args.DamageDelta).Float() < ent.Comp.DamageBuzzThreshold)
            return;

        if (_timing.CurTime < ent.Comp.NextBuzz)
            return;

        ent.Comp.NextBuzz = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.BuzzCooldown);
        ent.Comp.BuzzCount++;
        _audio.PlayPvs(ent.Comp.Sound, ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FishEmitBuzzWhileDamagedComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var buzz, out var mob))
        {
            if (TerminatingOrDeleted(uid) || _mobState.IsDead(uid, mob) || !_mobState.IsCritical(uid, mob))
            {
                buzz.CritEmoteScheduled = false;
                continue;
            }

            if (!buzz.CritEmoteScheduled)
            {
                ScheduleCritEmote(buzz, now);
                continue;
            }

            if (now < buzz.NextCritEmote)
                continue;

            PlayCritEmote(uid, buzz);
            ScheduleCritEmote(buzz, now);
        }
    }

    private void PlayCritEmote(EntityUid uid, FishEmitBuzzWhileDamagedComponent buzz)
    {
        if (buzz.CritEmotes.Count == 0)
            return;

        var emote = _random.Pick(buzz.CritEmotes);
        buzz.LastCritEmote = emote;
        // Крит блокирует обычные эмоции — этот сбой шасси должен проходить.
        _chat.TryEmoteWithChat(uid, emote, ignoreActionBlocker: true);
    }

    private void ScheduleCritEmote(FishEmitBuzzWhileDamagedComponent buzz, TimeSpan now)
    {
        var min = Math.Min(buzz.CritEmoteMinDelay, buzz.CritEmoteMaxDelay);
        var max = Math.Max(buzz.CritEmoteMinDelay, buzz.CritEmoteMaxDelay);
        // NextFloat не включает верхнюю границу. Зажимаем в [min, max].
        var delay = Math.Clamp(_random.NextFloat(min, max + 0.01f), min, max);
        buzz.LastCritEmoteDelay = delay;
        buzz.NextCritEmote = now + TimeSpan.FromSeconds(delay);
        buzz.CritEmoteScheduled = true;
    }

    private static FixedPoint2 PositiveDamage(DamageSpecifier damage)
    {
        var total = FixedPoint2.Zero;
        foreach (var value in damage.DamageDict.Values)
        {
            if (value > FixedPoint2.Zero)
                total += value;
        }

        return total;
    }
}
