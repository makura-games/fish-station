using Content.Shared._Fish.Crit; // Fish-edit: не эмоутить в Crit-unconscious
using Content.Shared.Damage.Systems;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Mobs.Systems; // Fish-edit
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Chat.Systems;

public sealed partial class EmoteOnDamageSystem : EntitySystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private MobStateSystem _mobState = default!; // Fish-edit

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmoteOnDamageComponent, DamageChangedEvent>(OnDamage);
    }

    private void OnDamage(EntityUid uid, EmoteOnDamageComponent emoteOnDamage, DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        // Fish-start: мёртвые / Crit-unconscious не кричат от урона
        if (_mobState.IsDead(uid) || HasComp<FishCritUnconsciousComponent>(uid))
            return;
        // Fish-end

        if (emoteOnDamage.LastEmoteTime + emoteOnDamage.EmoteCooldown > _gameTiming.CurTime)
            return;

        if (emoteOnDamage.Emotes.Count == 0)
            return;

        if (!_random.Prob(emoteOnDamage.EmoteChance))
            return;

        var emote = _random.Pick(emoteOnDamage.Emotes);
        if (emoteOnDamage.WithChat)
        {
            _chatSystem.TryEmoteWithChat(uid, emote, emoteOnDamage.HiddenFromChatWindow ? ChatTransmitRange.HideChat : ChatTransmitRange.Normal);
        }
        else
        {
            _chatSystem.TryEmoteWithoutChat(uid,emote);
        }

        emoteOnDamage.LastEmoteTime = _gameTiming.CurTime;
    }

    /// <summary>
    /// Try to add an emote to the entity, which will be performed at an interval.
    /// </summary>
    public bool AddEmote(EntityUid uid, string emotePrototypeId, EmoteOnDamageComponent? emoteOnDamage = null)
    {
        if (!Resolve(uid, ref emoteOnDamage, logMissing: false))
            return false;

        DebugTools.Assert(emoteOnDamage.LifeStage <= ComponentLifeStage.Running);
        DebugTools.Assert(ProtoMan.HasIndex<EmotePrototype>(emotePrototypeId), "Prototype not found. Did you make a typo?");

        return emoteOnDamage.Emotes.Add(emotePrototypeId);
    }

    /// <summary>
    /// Stop preforming an emote. Note that by default this will queue empty components for removal.
    /// </summary>
    public bool RemoveEmote(EntityUid uid, string emotePrototypeId, EmoteOnDamageComponent? emoteOnDamage = null, bool removeEmpty = true)
    {
        if (!Resolve(uid, ref emoteOnDamage, logMissing: false))
            return false;

        DebugTools.Assert(ProtoMan.HasIndex<EmotePrototype>(emotePrototypeId), "Prototype not found. Did you make a typo?");

        if (!emoteOnDamage.Emotes.Remove(emotePrototypeId))
            return false;

        if (removeEmpty && emoteOnDamage.Emotes.Count == 0)
            RemCompDeferred(uid, emoteOnDamage);

        return true;
    }
}
