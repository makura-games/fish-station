using Content.Shared._Fish.Mechs.Components;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Popups;
using Content.Shared.Radio.Components;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Fish.Mechs;

/// <summary>
/// Cabin internals + встроенное радио меха.
/// Компоненты радио уже на прототипе FishMechCore — только меняем каналы / флаги (без EnsureComp/RemComp).
/// </summary>
public abstract class SharedMechCabinRadioSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechCabinAtmosComponent, MechToggleInternalsEvent>(OnToggleInternals);
        SubscribeLocalEvent<MechRadioComponent, MechToggleRadioMicEvent>(OnToggleMic);
        SubscribeLocalEvent<MechRadioComponent, MechToggleRadioSpeakerEvent>(OnToggleSpeaker);
        SubscribeLocalEvent<MechRadioComponent, MapInitEvent>(OnRadioMapInit);
    }

    private void OnToggleInternals(Entity<MechCabinAtmosComponent> ent, ref MechToggleInternalsEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.UseInternalTank = !ent.Comp.UseInternalTank;
        Dirty(ent);
        _actions.SetToggled(ent.Comp.ToggleActionEntity, ent.Comp.UseInternalTank);
        _popup.PopupClient(
            Loc.GetString(ent.Comp.UseInternalTank ? "mech-internals-on" : "mech-internals-off"),
            ent,
            args.Performer);
    }

    private void OnRadioMapInit(Entity<MechRadioComponent> ent, ref MapInitEvent args)
    {
        // Не трогаем comps во время ApplyingState / client init — иначе prediction reset падает.
        if (_timing.ApplyingState || _net.IsClient)
            return;

        ApplyRadioChannels(ent);
    }

    private void OnToggleMic(Entity<MechRadioComponent> ent, ref MechToggleRadioMicEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.MicrophoneEnabled = !ent.Comp.MicrophoneEnabled;
        Dirty(ent);
        _actions.SetToggled(ent.Comp.ToggleMicActionEntity, ent.Comp.MicrophoneEnabled);
        if (!_timing.ApplyingState)
            ApplyRadioChannels(ent);
        _popup.PopupClient(
            Loc.GetString(ent.Comp.MicrophoneEnabled ? "mech-radio-mic-on" : "mech-radio-mic-off"),
            ent,
            args.Performer);
    }

    private void OnToggleSpeaker(Entity<MechRadioComponent> ent, ref MechToggleRadioSpeakerEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.SpeakerEnabled = !ent.Comp.SpeakerEnabled;
        Dirty(ent);
        _actions.SetToggled(ent.Comp.ToggleSpeakerActionEntity, ent.Comp.SpeakerEnabled);
        if (!_timing.ApplyingState)
            ApplyRadioChannels(ent);
        _popup.PopupClient(
            Loc.GetString(ent.Comp.SpeakerEnabled ? "mech-radio-speaker-on" : "mech-radio-speaker-off"),
            ent,
            args.Performer);
    }

    /// <summary>
    /// Вкл/выкл через каналы на уже существующих компонентах (без add/remove).
    /// </summary>
    private void ApplyRadioChannels(Entity<MechRadioComponent> ent)
    {
        if (TryComp(ent, out IntrinsicRadioTransmitterComponent? transmitter))
        {
            transmitter.Channels.Clear();
            if (ent.Comp.MicrophoneEnabled)
                transmitter.Channels.Add(SharedChatSystem.CommonChannel);
            Dirty(ent.Owner, transmitter);
        }

        if (TryComp(ent, out ActiveRadioComponent? active))
        {
            active.Channels.Clear();
            if (ent.Comp.SpeakerEnabled)
                active.Channels.Add(SharedChatSystem.CommonChannel);
            Dirty(ent.Owner, active);
        }
    }
}
