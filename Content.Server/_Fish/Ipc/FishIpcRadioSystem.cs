using Content.Server.Radio.EntitySystems;
using Content.Shared._Fish.Ipc;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Starlight.Medical.Surgery;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Ipc;

/// <summary>
/// Встроенная рация КПБ: общий и двоичный каналы плюс ключи шифрования в корпусе.
/// Без заряда приём и передача отключаются.
/// </summary>
public sealed partial class FishIpcRadioSystem : EntitySystem
{
    private static readonly ProtoId<RadioChannelPrototype> BinaryChannel = "Binary";
    private static readonly ProtoId<EmoteSoundsPrototype> IpcVoice = "UnisexIpc";

    [Dependency] private VocalSystem _vocal = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishIpcComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FishIpcComponent, VoiceChangedEvent>(OnVoiceChanged, after: [typeof(VocalSystem)]);
        SubscribeLocalEvent<FishIpcComponent, EncryptionChannelsChangedEvent>(OnKeysChanged);
        SubscribeLocalEvent<FishIpcComponent, GetDefaultRadioChannelEvent>(OnGetDefault);
        SubscribeLocalEvent<FishIpcComponent, EntitySpokeEvent>(OnSpoke, before: [typeof(RadioSystem)]);
    }

    private void OnMapInit(Entity<FishIpcComponent> ent, ref MapInitEvent args)
    {
        // Приказ: хирургия на КПБ выключена, модули остаются.
        RemCompDeferred<SurgeryTargetComponent>(ent.Owner);
        LockIpcVoice(ent);

        SetRadioEnabled(ent, !ent.Comp.PowerDown);
    }

    private void OnVoiceChanged(Entity<FishIpcComponent> ent, ref VoiceChangedEvent args)
    {
        // Профиль человека затирает UnisexIpc: у Buzz-Two и Ping тогда нет ключа.
        LockIpcVoice(ent);
    }

    private void LockIpcVoice(Entity<FishIpcComponent> ent)
    {
        if (!TryComp<VocalComponent>(ent, out var vocal) || vocal.EmoteSounds == IpcVoice)
            return;

        _vocal.SetEmoteSounds((ent.Owner, vocal), IpcVoice);
    }

    private void OnKeysChanged(Entity<FishIpcComponent> ent, ref EncryptionChannelsChangedEvent args)
    {
        if (ent.Comp.PowerDown)
            return;

        SetRadioEnabled(ent, true);
    }

    private void OnGetDefault(Entity<FishIpcComponent> ent, ref GetDefaultRadioChannelEvent args)
    {
        if (args.Channel != null)
            return;

        // При нуле заряда рация молчит, вокальный модуль это не обходит.
        if (ent.Comp.PowerDown)
            return;

        if (TryComp<EncryptionKeyHolderComponent>(ent, out var keys) && keys.DefaultChannel != null)
        {
            args.Channel = keys.DefaultChannel;
            return;
        }

        args.Channel = SharedChatSystem.CommonChannel;
    }

    private void OnSpoke(Entity<FishIpcComponent> ent, ref EntitySpokeEvent args)
    {
        if (!ent.Comp.PowerDown || args.Channel == null)
            return;

        // Без заряда префикс рации не уходит в эфир, даже с вокальным модулятором.
        args.Channel = null;
    }

    public void SetRadioEnabled(Entity<FishIpcComponent> ent, bool enabled)
    {
        if (TerminatingOrDeleted(ent.Owner) || !TryComp<IntrinsicRadioTransmitterComponent>(ent, out var transmitter))
            return;

        if (!enabled)
        {
            transmitter.Channels.Clear();
            RemComp<ActiveRadioComponent>(ent.Owner);
            return;
        }

        var channels = new HashSet<ProtoId<RadioChannelPrototype>>
        {
            SharedChatSystem.CommonChannel,
            BinaryChannel,
        };

        if (TryComp<EncryptionKeyHolderComponent>(ent, out var keys))
            channels.UnionWith(keys.Channels);

        transmitter.Channels = channels;
        EnsureComp<ActiveRadioComponent>(ent.Owner).Channels = channels;
    }
}
