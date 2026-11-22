using Content.Server._Sunrise.Messenger;
using Content.Server.Destructible;
using Content.Server.Pinpointer;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared._Fish.Trigger;
using Content.Shared._Sunrise.TTS;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Lock;
using Content.Shared.Radio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Fish.Trigger;

/// <summary>
/// Отслеживает получение урона сущностями с <see cref="RattleOnDamageComponent"/>
/// и отправляет в заданный канал связи предупреждение о получении урона с ближайшим маяком.
/// </summary>
public sealed partial class RattleOnDamageSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MessengerServerSystem _messenger = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private EntityQuery<LockComponent> _lockQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

#pragma warning disable CS0618 // Отслеживаем итоговое состояние DamageableComponent после любого изменения урона/починки
        SubscribeLocalEvent<RattleOnDamageComponent, DamageChangedEvent>(
            OnDamageChanged,
            before: [typeof(DestructibleSystem)]);
#pragma warning restore CS0618
    }

#pragma warning disable CS0618
    private void OnDamageChanged(Entity<RattleOnDamageComponent> ent, ref DamageChangedEvent args)
#pragma warning restore CS0618
    {
        var totalDamage = GetTotalDamage((ent.Owner, args.Damageable));

        if (totalDamage < ent.Comp.DamageThreshold)
        {
            if (ent.Comp.ResetOnHeal)
                ent.Comp.Triggered = false;

            return;
        }

        TrySendAlert((ent.Owner, ent.Comp), args.Damageable, args.DamageIncreased);
    }

    /// <summary>
    /// Пытается отправить сообщение о превышении порога урона в выбранный канал связи.
    /// </summary>
    public bool TrySendAlert(
        Entity<RattleOnDamageComponent?> ent,
        DamageableComponent? damageable = null,
        bool damageIncreased = true)
    {
        if (TerminatingOrDeleted(ent.Owner))
            return false;

        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!CanSendAlert(ent, damageable, damageIncreased))
            return false;

        ent.Comp.Triggered = true;
        ent.Comp.NextAlertTime = _timing.CurTime + ent.Comp.Cooldown;

        var posText = FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(ent.Owner));
        var message = Loc.GetString(ent.Comp.Message, ("entity", ent.Owner), ("position", posText));

        EntityUid? messengerServer = null;
        string? messengerMessage = null;
        if (ent.Comp.SendToMessenger &&
            _messenger.GetServerEntity(_station.GetOwningStation(ent.Owner)) is var (server, _))
        {
            messengerServer = server;
            messengerMessage = Loc.GetString("rattle-on-damage-messenger-wrap", ("entity", ent.Owner), ("message", message));
        }

        foreach (var channelId in ent.Comp.RadioChannels)
        {
            if (messengerServer is { } serverUid &&
                messengerMessage != null &&
                _messenger.GetGroupIdByRadioChannel(channelId) is { } groupId)
            {
                _messenger.SendSystemMessageToGroup(serverUid, groupId, messengerMessage);
                continue;
            }

            if (ProtoMan.TryIndex(channelId, out var channel))
                SendRadioWithOptionalTts((ent.Owner, ent.Comp), message, channel);
        }

        if (ent.Comp.Sound != null)
            _audio.PlayPvs(ent.Comp.Sound, Transform(ent.Owner).Coordinates);

        return true;
    }

    /// <summary>
    /// Проверяет может ли сущность сейчас отправить предупреждение о превышении порога урона.
    /// </summary>
    public bool CanSendAlert(
        Entity<RattleOnDamageComponent?> ent,
        DamageableComponent? damageable = null,
        bool damageIncreased = true)
    {
        if (TerminatingOrDeleted(ent.Owner))
            return false;

        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!Resolve(ent.Owner, ref damageable, false))
            return false;

        if (!damageIncreased || ent.Comp.Triggered)
            return false;

        if (ent.Comp.IgnoreWhenUnlocked &&
            _lockQuery.TryComp(ent.Owner, out var lockComp) &&
            !lockComp.Locked)
        {
            return false;
        }

        if (_timing.CurTime < ent.Comp.NextAlertTime)
            return false;

        return GetTotalDamage((ent.Owner, damageable)) >= ent.Comp.DamageThreshold;
    }

    private FixedPoint2 GetTotalDamage(Entity<DamageableComponent> ent)
    {
        return _damageable.GetPositiveDamage(ent).GetTotal();
    }

    private void SendRadioWithOptionalTts(Entity<RattleOnDamageComponent> ent, string message, RadioChannelPrototype channel)
    {
        if (ent.Comp.TtsVoice is not { } voiceId)
        {
            _radio.SendRadioMessage(ent.Owner, message, channel, ent.Owner);
            return;
        }

        var hadTts = TryComp<TTSComponent>(ent.Owner, out var existingTts);
        var ttsComp = hadTts ? existingTts! : AddComp<TTSComponent>(ent.Owner);
        if (!hadTts)
            ttsComp.VoicePrototypeId = voiceId;

        try
        {
            _radio.SendRadioMessage(ent.Owner, message, channel, ent.Owner);
        }
        finally
        {
            if (!hadTts)
                RemComp<TTSComponent>(ent.Owner);
        }
    }
}
