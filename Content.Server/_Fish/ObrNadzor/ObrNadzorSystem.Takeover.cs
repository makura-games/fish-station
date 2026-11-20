using Content.Shared._Fish.ObrNadzor;
using Content.Shared.Chat;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Utility;

namespace Content.Server._Fish.ObrNadzor;

public sealed partial class ObrNadzorSystem
{
    private void OnTakeoverMsg(Entity<ObrNadzorConsoleComponent> ent, ref ObrNadzorTakeoverBuiMsg args)
    {
        TryStartTakeover(ent, args.Actor);
    }

    private void OnReleaseMsg(Entity<ObrNadzorConsoleComponent> ent, ref ObrNadzorReleaseSessionBuiMsg args)
    {
        if (ent.Comp.RemoteEntity == null)
            return;

        // Снять контроль: сам оператор, либо другой с доступом ЦК.
        var isOperator = args.Actor == ent.Comp.OperatorBody;
        if (!isOperator && !_access.IsAllowed(args.Actor, ent))
            return;

        EndSession(ent);
    }

    private void OnReleaseAction(ObrNadzorReleaseSessionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ObrNadzorSessionComponent>(args.Performer, out var session))
            return;

        if (!TryComp(session.Console, out ObrNadzorConsoleComponent? console))
            return;

        EndSession((session.Console, console));
        args.Handled = true;
    }

    private void OnSessionShutdown(Entity<ObrNadzorSessionComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Ending)
            return;

        if (!TryComp(ent.Comp.Console, out ObrNadzorConsoleComponent? console))
            return;

        EndSession((ent.Comp.Console, console), fromSession: true);
    }

    private void OnSessionTerminating(Entity<ObrNadzorSessionComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Ending)
            return;

        if (!TryComp(ent.Comp.Console, out ObrNadzorConsoleComponent? console))
            return;

        EndSession((ent.Comp.Console, console), fromSession: true);
    }

    private void OnEyeTerminating(Entity<ObrNadzorEyeComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!TryComp(ent.Comp.Console, out ObrNadzorConsoleComponent? console))
            return;

        // Уже чистимся из EndSession — повторный заход не нужен.
        if (console.OperatorBody == null && console.RemoteEntity == null)
            return;

        // Глаз уже удаляется — EndSession не должен QueueDel его повторно.
        if (console.RemoteEntity != null && TryGetEntity(console.RemoteEntity, out var eye) && eye == ent.Owner)
            console.RemoteEntity = null;

        EndSession((ent.Comp.Console, console));
    }

    private void TryStartTakeover(Entity<ObrNadzorConsoleComponent> ent, EntityUid actor)
    {
        if (ent.Comp.RemoteEntity != null)
        {
            _popup.PopupEntity(Loc.GetString("obr-nadzor-session-active"), actor, actor);
            return;
        }

        // Одна сессия на оператора — нельзя взять вторую консоль параллельно.
        if (HasComp<ObrNadzorSessionComponent>(actor))
        {
            _popup.PopupEntity(Loc.GetString("obr-nadzor-session-active"), actor, actor);
            return;
        }

        if (ent.Comp.Opener != actor || ent.Comp.TargetBeacon is not { } beacon)
        {
            _popup.PopupEntity(Loc.GetString("obr-nadzor-need-observe"), actor, actor);
            return;
        }

        if (!TryValidateBeacon(ent, beacon, out var station))
        {
            _popup.PopupEntity(Loc.GetString("obr-nadzor-no-station"), actor, actor);
            ClearTargeting(ent);
            RefreshUiState(ent);
            return;
        }

        // Текст всей станции без TTS/звука чата, затем блэкаут и глаз.
        AnnounceNadzor(station.Value, ent.Comp);
        BeginStationBlackout(station.Value, ent.Comp.BlackoutDuration);

        var eye = SpawnAtPosition(ent.Comp.RemoteEntityProto, Transform(beacon).Coordinates);
        ent.Comp.RemoteEntity = GetNetEntity(eye);

        EnsureComp<VisibilityComponent>(eye);
        var eyeMarker = EnsureComp<ObrNadzorEyeComponent>(eye);
        eyeMarker.Console = ent.Owner;
        Dirty(eye, eyeMarker);

        AddVirtualItems(actor, ent);
        SetEye(actor, eye);

        var session = EnsureComp<ObrNadzorSessionComponent>(actor);
        session.Console = ent.Owner;
        session.Ending = false;
        EntityUid? releaseAction = null;
        _actions.AddAction(actor, ref releaseAction, ReleaseAction);
        session.ReleaseAction = releaseAction;
        Dirty(actor, session);

        ent.Comp.OperatorBody = actor;
        ent.Comp.Opener = null;
        ent.Comp.TargetStation = station;
        Dirty(ent);
        RefreshUiState(ent);
    }

    private void EndSession(Entity<ObrNadzorConsoleComponent> ent, bool fromSession = false)
    {
        var body = ent.Comp.OperatorBody;
        var remote = ent.Comp.RemoteEntity;

        // Идемпотентность: повторный вызов (eye terminate / session shutdown) — no-op.
        if (body == null && remote == null && ent.Comp.Opener == null && ent.Comp.TargetBeacon == null)
            return;

        if (body != null && Exists(body.Value) && TryComp(body.Value, out ObrNadzorSessionComponent? session))
            session.Ending = true;

        if (body != null && Exists(body.Value))
        {
            RemoveEye(body.Value);
            _virtualItem.DeleteInHandsMatching(body.Value, ent.Owner);

            if (TryComp(body.Value, out ObrNadzorSessionComponent? sessionComp))
            {
                _actions.RemoveAction(body.Value, sessionComp.ReleaseAction);
                if (!fromSession)
                    RemCompDeferred<ObrNadzorSessionComponent>(body.Value);
            }
        }

        ent.Comp.RemoteEntity = null;
        ent.Comp.OperatorBody = null;
        ent.Comp.Opener = null;
        ent.Comp.TargetBeacon = null;
        ent.Comp.TargetStation = null;
        Dirty(ent);

        if (remote != null && TryGetEntity(remote, out var eye) && Exists(eye.Value))
            QueueDel(eye.Value);

        // Консоль может уже удаляться — UI только если ещё жива.
        if (Exists(ent.Owner) && !Terminating(ent.Owner))
            RefreshUiState(ent);
    }

    /// <summary>
    /// Текстовое объявление на всю станцию: без TTS, без звука чата и без зависимости от живых динамиков.
    /// </summary>
    private void AnnounceNadzor(EntityUid station, ObrNadzorConsoleComponent console)
    {
        if (!TryComp<StationDataComponent>(station, out var stationData))
            return;

        var sender = Loc.GetString("obr-nadzor-announcement-sender");
        var message = Loc.GetString("obr-nadzor-announcement");
        var wrapped = Loc.GetString(
            "chat-manager-sender-announcement-wrap-message",
            ("sender", sender),
            ("message", FormattedMessage.EscapeText(message)));

        var filter = _station.GetInStation(stationData);
        _chatManager.ChatMessageToManyFiltered(
            filter,
            ChatChannel.Radio,
            message,
            wrapped,
            station,
            hideChat: false,
            recordReplay: true,
            colorOverride: console.AnnouncementColor,
            audioPath: null,
            audioVolume: 0f);
    }
}
