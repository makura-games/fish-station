using Content.Server.Radio.EntitySystems;
using Content.Shared._Fish.Ipc;
using Content.Shared.Chat;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Speech;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Ipc;

/// <summary>
/// Встроенная рация КПБ: общий и двоичный каналы плюс ключи шифрования в корпусе.
/// Без заряда приём и передача отключаются.
/// </summary>
public sealed partial class FishIpcRadioSystem : EntitySystem
{
    private static readonly ProtoId<RadioChannelPrototype> BinaryChannel = "Binary";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishIpcComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FishIpcComponent, EncryptionChannelsChangedEvent>(OnKeysChanged);
        SubscribeLocalEvent<FishIpcComponent, GetDefaultRadioChannelEvent>(OnGetDefault);
        SubscribeLocalEvent<FishIpcComponent, EntitySpokeEvent>(OnSpoke, before: [typeof(RadioSystem)]);
    }

    private void OnMapInit(Entity<FishIpcComponent> ent, ref MapInitEvent args)
    {
        SetRadioEnabled(ent, !ent.Comp.PowerDown);
    }

    private void OnKeysChanged(Entity<FishIpcComponent> ent, ref EncryptionChannelsChangedEvent args)
    {
        if (ent.Comp.PowerDown)
            return;

        SetRadioEnabled(ent, true);
    }

    private void OnGetDefault(Entity<FishIpcComponent> ent, ref GetDefaultRadioChannelEvent args)
    {
        if (args.Channel != null || ent.Comp.PowerDown)
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

        // Без заряда префикс рации не уходит в эфир.
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
