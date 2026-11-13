using Content.Server.Chat.Systems;
using Content.Shared._Fish.CameraHearing;
using Content.Shared.Chat;
using Content.Shared.Interaction;
using Content.Shared.Examine;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.SurveillanceCamera;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared.Xenoborgs.Components;
using Robust.Shared.Player;

namespace Content.Server._Fish.CameraHearing;

/// <summary>
/// Речь рядом с улучшенной камерой или активным боргом слышит ИИ. Стены глушат звук.
/// </summary>
public sealed partial class FishCameraHearingSystem : EntitySystem
{
    [Dependency] private SharedStationAiSystem _stationAi = default!;
    [Dependency] private SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private readonly List<EntityUid> _heardBy = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EntitySpokeEvent>(OnSpoke);
        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnExpandRecipients);
        SubscribeLocalEvent<BorgChassisComponent, ComponentInit>(OnBorgInit);
        SubscribeLocalEvent<FishCameraUpgradeComponent, AfterInteractEvent>(OnUpgradeInteract);
    }

    private void OnBorgInit(Entity<BorgChassisComponent> ent, ref ComponentInit args)
    {
        if (!HasComp<BorgTransponderComponent>(ent) || HasComp<XenoborgComponent>(ent))
            return;

        EnsureComp<FishCameraHearingComponent>(ent);
    }

    private void OnUpgradeInteract(Entity<FishCameraUpgradeComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<SurveillanceCameraComponent>(target))
            return;

        args.Handled = true;

        if (HasComp<FishCameraHearingComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.AlreadyUpgradedPopup), target, args.User);
            return;
        }

        AddComp<FishCameraHearingComponent>(target);
        _popup.PopupEntity(Loc.GetString(ent.Comp.UpgradedPopup), target, args.User);
        QueueDel(ent);
    }

    private void OnSpoke(EntitySpokeEvent ev)
    {
        var whisper = ev.ObfuscatedMessage != null;

        if (!HasComp<MindContainerComponent>(ev.Source) || Hear(ev.Source, whisper) == null)
            return;

        var nameEv = new TransformSpeakerNameEvent(ev.Source, MetaData(ev.Source).EntityName);
        RaiseLocalEvent(ev.Source, nameEv);
        var message = new FishCameraSpeechMessage(GetNetEntity(ev.Source), nameEv.VoiceName, ev.Message, whisper);

        foreach (var camera in _heardBy)
        {
            if (!TryComp<SurveillanceCameraComponent>(camera, out var comp))
                continue;

            foreach (var monitor in comp.ActiveMonitors)
            {
                _ui.ServerSendUiMessage(monitor, SurveillanceCameraMonitorUiKey.Key, message);
            }
        }
    }

    private void OnExpandRecipients(ExpandICChatRecipientsEvent ev)
    {
        if (!HasComp<MindContainerComponent>(ev.Source))
            return;

        var whisper = ev.VoiceRange <= SharedChatSystem.WhisperMuffledRange;
        var nearest = Hear(ev.Source, whisper);
        var sourceCoords = _transform.GetMapCoordinates(ev.Source);

        var cores = EntityQueryEnumerator<StationAiCoreComponent>();
        while (cores.MoveNext(out var core, out var coreComp))
        {
            if (!coreComp.Remote ||
                !_stationAi.TryGetHeld((core, coreComp), out var held) ||
                !TryComp<ActorComponent>(held, out var actor) ||
                held == ev.Source)
                continue;

            var session = actor.PlayerSession;

            if (session.AttachedEntity is { } eye &&
                eye == coreComp.RemoteEntity &&
                ev.Recipients.ContainsKey(session) &&
                !_examine.InRangeUnOccluded(eye, sourceCoords, 0f))
                ev.Recipients.Remove(session);

            if (nearest != null)
                ev.Recipients.TryAdd(session, new ChatSystem.ICChatRecipientData(nearest.Value, false, !whisper));
        }
    }

    /// <summary>
    /// Находит работающие источники слуха, которые слышат говорящего. Возвращает расстояние до ближайшего.
    /// </summary>
    private float? Hear(EntityUid source, bool whisper)
    {
        var sourceXform = Transform(source);
        var sourcePos = _transform.GetWorldPosition(sourceXform);
        var sourceCoords = _transform.GetMapCoordinates(sourceXform);
        float? nearest = null;

        _heardBy.Clear();

        var query = EntityQueryEnumerator<FishCameraHearingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var hearing, out var xform))
        {
            if (xform.MapID != sourceXform.MapID || !IsWorking(uid))
                continue;

            var distance = (_transform.GetWorldPosition(xform) - sourcePos).Length();

            if (distance > (whisper ? hearing.WhisperRange : hearing.Range) ||
                !_examine.InRangeUnOccluded(_transform.GetMapCoordinates(xform), sourceCoords, 0f, null))
                continue;

            _heardBy.Add(uid);
            nearest = nearest == null ? distance : Math.Min(nearest.Value, distance);
        }

        return nearest;
    }

    private bool IsWorking(EntityUid uid)
    {
        if (TryComp<BorgChassisComponent>(uid, out var borg) && !borg.Active)
            return false;

        return !TryComp<SurveillanceCameraComponent>(uid, out var camera) ||
               camera.Active && _powerReceiver.IsPowered(uid);
    }
}
