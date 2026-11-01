using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.Mind.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Silicons.StationAi;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared.Wall;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Fish.Silicons.StationAi;

/// <summary>
/// ИИ в режиме полёта слышит речь в радиусе от любой работающей камеры, если между ними нет стены.
/// </summary>
public sealed partial class FishAiCameraHearingSystem : EntitySystem
{
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private SharedStationAiSystem _stationAi = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedTransformSystem _xforms = default!;
    [Dependency] private ExamineSystemShared _examine = default!;

    private const float HearRange = 5f;

    private const float WallMountFrontOffset = 0.75f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EntitySpokeEvent>(OnSpoke);
    }

    private void OnSpoke(EntitySpokeEvent ev)
    {
        if (!HasComp<MindContainerComponent>(ev.Source))
            return;

        var whisper = ev.ObfuscatedMessage != null;
        var sourceXform = Transform(ev.Source);
        var distance = GetDistanceToNearestCamera(sourceXform);

        if (distance == null || whisper && distance > SharedChatSystem.WhisperMuffledRange)
            return;

        var message = whisper && distance > SharedChatSystem.WhisperClearRange
            ? ev.ObfuscatedMessage!
            : ev.Message;

        var directRange = whisper ? SharedChatSystem.WhisperMuffledRange : SharedChatSystem.VoiceRange;
        var query = EntityQueryEnumerator<StationAiCoreComponent>();
        while (query.MoveNext(out var core, out var coreComp))
        {
            if (!coreComp.Remote ||
                !_stationAi.TryGetHeld((core, coreComp), out var held) ||
                !TryComp<ActorComponent>(held, out var actor) ||
                held == ev.Source)
                continue;

            if (_xforms.InRange((held.Value, Transform(held.Value)), (ev.Source, sourceXform), directRange))
                continue;

            SendToAi(ev.Source, message, whisper, actor.PlayerSession);
        }
    }

    private float? GetDistanceToNearestCamera(TransformComponent sourceXform)
    {
        var sourcePos = _xforms.GetWorldPosition(sourceXform);
        var sourceCoords = _xforms.GetMapCoordinates(sourceXform);
        float? nearest = null;

        var query = EntityQueryEnumerator<SurveillanceCameraComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var camera, out var xform))
        {
            if (!camera.Active || xform.MapID != sourceXform.MapID || !_power.IsPowered(uid))
                continue;

            var distance = (_xforms.GetWorldPosition(xform) - sourcePos).Length();

            if (distance > HearRange || nearest != null && distance >= nearest)
                continue;

            if (IsBlockedByWall(uid, xform, sourceCoords))
                continue;

            nearest = distance;
        }

        return nearest;
    }

    private bool IsBlockedByWall(EntityUid camera, TransformComponent cameraXform, MapCoordinates source)
    {
        var origin = _xforms.GetMapCoordinates(cameraXform);

        if (TryComp<WallMountComponent>(camera, out var mount))
        {
            var front = (_xforms.GetWorldRotation(cameraXform) + mount.Direction).RotateVec(new Vector2(0f, -1f));
            origin = origin.Offset(front * WallMountFrontOffset);
        }

        return !_examine.InRangeUnOccluded(origin, source, 0f, null);
    }

    private void SendToAi(EntityUid source, string message, bool whisper, ICommonSession session)
    {
        var nameEv = new TransformSpeakerNameEvent(source, MetaData(source).EntityName);
        RaiseLocalEvent(source, nameEv);

        var name = FormattedMessage.EscapeText(nameEv.VoiceName);
        var content = FormattedMessage.EscapeText(message);
        string wrapped;

        if (whisper)
        {
            wrapped = Loc.GetString("chat-manager-entity-whisper-wrap-message", ("entityName", name), ("message", content));
        }
        else
        {
            var speech = nameEv.SpeechVerb != null && ProtoMan.Resolve(nameEv.SpeechVerb, out var verbProto)
                ? verbProto
                : _chat.GetSpeechVerb(source, message);

            wrapped = Loc.GetString(speech.Bold ? "chat-manager-entity-say-bold-wrap-message" : "chat-manager-entity-say-wrap-message",
                ("entityName", name),
                ("verb", Loc.GetString(_random.Pick(speech.SpeechVerbStrings))),
                ("fontType", speech.FontId),
                ("fontSize", speech.FontSize),
                ("message", content));
        }

        _chatManager.ChatMessageToOne(whisper ? ChatChannel.Whisper : ChatChannel.Local, message, wrapped, source, false, session.Channel);
    }
}
