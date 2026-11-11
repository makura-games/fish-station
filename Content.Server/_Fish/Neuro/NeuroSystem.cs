using System;
using System.Collections.Generic;
using System.Text;
using Content.Server._Sunrise.Chat.Sanitization;
using Content.Server._Fish.MentalHealth;
using Content.Shared._Fish.MentalHealth;
using Content.Shared._Fish.Neuro;
using Content.Shared._Fish.Stress;
using Content.Shared.Chat;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Drunk;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Content.Shared.StatusEffectNew;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Neuro;

public sealed partial class NeuroSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> AsphyxiationDamageType = "Asphyxiation";
    private static readonly ProtoId<DamageTypePrototype> PoisonDamageType = "Poison";
    private static readonly EntProtoId CriticalSlowdown = "NeuroCriticalSlowdownStatusEffect";
    private static readonly EntProtoId ModerateSlowdown = "FishNeuroModerateSlowdownStatusEffect";
    private static readonly string RussianVowels = "аеёиоуыэюяАЕЁИОУЫЭЮЯ";
    private static readonly char[] ReplacementRussianVowels = "аеёиоуыэюя".ToCharArray();
    private static readonly string EnglishVowels = "aeiouAEIOU";
    private static readonly char[] ReplacementEnglishVowels = "aeiou".ToCharArray();
    private static readonly string[] MildAdditions = { "!", "?", "😨" };

    [Dependency] private MentalHealthConfigSystem _config = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private MovementModStatusSystem _movementMod = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    private float _regenTimer;
    private readonly Dictionary<EntityUid, TimeSpan> _criticalAlertCooldowns = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextStumble = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<ActorComponent, TrySendChatMessageEvent>(OnTrySendChatMessage);
        SubscribeLocalEvent<NeuroComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<NeuroComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<NeuroComponent, ComponentShutdown>(OnNeuroShutdown);
    }

    private void OnTrySendChatMessage(Entity<ActorComponent> ent, ref TrySendChatMessageEvent args)
    {
        if (args.Cancelled ||
            args.IcChatType == null ||
            !TryComp<NeuroComponent>(ent.Owner, out var neuro))
            return;

        if (neuro.Current < MentalHealthConstants.NeuroCritical)
        {
            args.Cancelled = true;
        }
        else if (neuro.Current < MentalHealthConstants.NeuroModerate)
        {
            args.Message = DistortSeverely(args.Message);
        }
        else if (neuro.Current < MentalHealthConstants.NeuroMild)
        {
            args.Message = DistortMildly(args.Message);
        }
    }

    private void OnInteractionAttempt(Entity<NeuroComponent> ent, ref InteractionAttemptEvent args)
    {
        if (ent.Comp.Current < MentalHealthConstants.NeuroCritical)
            args.Cancelled = true;
    }

    private void OnDamageChanged(Entity<NeuroComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null)
            return;

        var delta = args.DamageDelta.DamageDict;
        var asphyxiation = Math.Max(0f, delta.GetValueOrDefault(AsphyxiationDamageType).Float());
        var poison = Math.Max(0f, delta.GetValueOrDefault(PoisonDamageType).Float());
        var neuralDamage = (asphyxiation + poison) * 0.1f;
        if (neuralDamage <= 0f)
            return;

        ent.Comp.Current = Math.Max(0f, ent.Comp.Current - neuralDamage);
        Dirty(ent);
        ApplyNeuroEffects(ent, ent.Comp);
        if (neuralDamage >= 1f)
            RaiseNetworkEvent(new NeuroAlertEvent("fish-neuro-muddling"), Filter.Entities(ent));
    }

    private void OnEntitySpoke(EntitySpokeEvent ev)
    {
        if (!TryComp<NeuroComponent>(ev.Source, out var neuro))
            return;

        var message = ev.Message;
        int found = 0;
        foreach (var word in _config.ForbiddenWords)
        {
            found += CountOccurrences(message, word);
        }

        if (found > 0)
        {
            var loss = 0f;
            for (var i = 0; i < found; i++)
                loss += _random.NextFloat(1f, 2f);

            neuro.Current = Math.Max(0f, neuro.Current - loss);
            Dirty(ev.Source, neuro);
            ApplyNeuroEffects(ev.Source, neuro);
            RaiseNetworkEvent(
                new NeuroAlertEvent("fish-neuro-muddling"),
                Filter.Entities(ev.Source));
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _regenTimer += frameTime;
        var elapsedSeconds = (int) _regenTimer;
        if (elapsedSeconds == 0)
            return;
        _regenTimer -= elapsedSeconds;

        var query = EntityQueryEnumerator<NeuroComponent, StressComponent>();
        while (query.MoveNext(out var uid, out var neuro, out var stress))
        {
            bool inCombat = false;
            if (TryComp<CombatModeComponent>(uid, out var combat))
                inCombat = combat.IsInCombatMode;

            if (stress.Current < neuro.RegenStressThreshold && !inCombat)
            {
                neuro.Current = Math.Min(
                    neuro.Max - neuro.PermanentLoss,
                    neuro.Current + neuro.NaturalRegenRate * elapsedSeconds);
                Dirty(uid, neuro);
            }

            ApplyNeuroEffects(uid, neuro);
            UpdateCriticalAlert(uid, neuro);
        }
    }

    private void ApplyNeuroEffects(EntityUid uid, NeuroComponent neuro)
    {
        if (neuro.Current < MentalHealthConstants.NeuroCritical)
        {
            EnsureComp<NeuroCriticalComponent>(uid);
        }
        else
        {
            RemComp<NeuroCriticalComponent>(uid);
        }

        if (neuro.Current < MentalHealthConstants.NeuroModerate)
        {
            _statusEffects.TryAddStatusEffectDuration(uid, SharedDrunkSystem.Drunk, TimeSpan.FromSeconds(1.5));
        }
        else
        {
            _statusEffects.TryRemoveStatusEffect(uid, SharedDrunkSystem.Drunk);
        }

        if (neuro.Current < MentalHealthConstants.NeuroCritical)
        {
            _movementMod.TryAddMovementSpeedModDuration(
                uid,
                CriticalSlowdown,
                TimeSpan.FromSeconds(1.5),
                0.35f);
            _statusEffects.TryRemoveStatusEffect(uid, ModerateSlowdown);
        }
        else if (neuro.Current < MentalHealthConstants.NeuroModerate)
        {
            _statusEffects.TryRemoveStatusEffect(uid, CriticalSlowdown);
            _movementMod.TryAddMovementSpeedModDuration(
                uid,
                ModerateSlowdown,
                TimeSpan.FromSeconds(1.5),
                0.65f);
        }
        else
        {
            _statusEffects.TryRemoveStatusEffect(uid, CriticalSlowdown);
            _statusEffects.TryRemoveStatusEffect(uid, ModerateSlowdown);
        }

        UpdateStumble(uid, neuro);
    }

    private void UpdateStumble(EntityUid uid, NeuroComponent neuro)
    {
        if (neuro.Current >= MentalHealthConstants.NeuroMild)
        {
            _nextStumble.Remove(uid);
            return;
        }

        var now = _timing.CurTime;
        if (_nextStumble.TryGetValue(uid, out var next) && now < next)
            return;

        var (minimum, maximum) = neuro.Current switch
        {
            < MentalHealthConstants.NeuroCritical => (4f, 8f),
            < MentalHealthConstants.NeuroModerate => (20f, 40f),
            _ => (60f, 120f),
        };

        _stun.TryKnockdown(
            uid,
            TimeSpan.FromSeconds(0.2),
            refresh: false,
            autoStand: true,
            drop: false);
        _nextStumble[uid] = now + TimeSpan.FromSeconds(_random.NextFloat(minimum, maximum));
    }

    private void UpdateCriticalAlert(EntityUid uid, NeuroComponent neuro)
    {
        if (neuro.Current >= MentalHealthConstants.NeuroCritical)
        {
            _criticalAlertCooldowns.Remove(uid);
            return;
        }

        if (_criticalAlertCooldowns.TryGetValue(uid, out var nextAlert) && _timing.CurTime < nextAlert)
            return;

        _criticalAlertCooldowns[uid] = _timing.CurTime + TimeSpan.FromSeconds(15);
        RaiseNetworkEvent(new NeuroAlertEvent("fish-neuro-critical"), Filter.Entities(uid));
    }

    private void OnNeuroShutdown(EntityUid uid, NeuroComponent component, ComponentShutdown args)
    {
        _criticalAlertCooldowns.Remove(uid);
        _nextStumble.Remove(uid);
    }

    private static int CountOccurrences(string message, string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return 0;

        var count = 0;
        var searchIndex = 0;
        while (searchIndex < message.Length)
        {
            var index = message.IndexOf(word, searchIndex, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                break;

            var end = index + word.Length;
            var hasWordBoundaries =
                (index == 0 || !char.IsLetterOrDigit(message[index - 1])) &&
                (end == message.Length || !char.IsLetterOrDigit(message[end]));
            if (hasWordBoundaries)
                count++;

            searchIndex = end;
        }

        return count;
    }

    private string DistortMildly(string message)
    {
        var result = new StringBuilder(message.Length + 8);
        foreach (var character in message)
        {
            result.Append(character);
            if (_random.Prob(0.08f))
                result.Append(_random.Pick(MildAdditions));
        }

        return result.ToString();
    }

    private string DistortSeverely(string message)
    {
        var result = message.ToCharArray();

        for (var i = 0; i < result.Length; i++)
        {
            if (RussianVowels.Contains(result[i]) && _random.Prob(0.4f))
                result[i] = _random.Pick(ReplacementRussianVowels);
            else if (EnglishVowels.Contains(result[i]) && _random.Prob(0.4f))
                result[i] = _random.Pick(ReplacementEnglishVowels);
        }

        return new string(result);
    }
}