using Content.Shared._Fish.ObrNadzor;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Audio;
using Robust.Shared.Map;

namespace Content.Server._Fish.ObrNadzor;

public sealed partial class ObrNadzorSystem
{
    private void OnTakeoverMsg(Entity<ObrNadzorConsoleComponent> ent, ref ObrNadzorTakeoverBuiMsg args)
    {
        TryStartTakeover(ent, args.Actor);
    }

    private void OnReleaseMsg(Entity<ObrNadzorConsoleComponent> ent, ref ObrNadzorReleaseSessionBuiMsg args)
    {
        if (ent.Comp.SessionBrain is not { } brain || !Exists(brain))
            return;

        // Снять контроль может сам оператор (через AI) или любой с доступом у консоли.
        if (args.Actor != brain && !_access.IsAllowed(args.Actor, ent))
            return;

        EndSession(ent, restoreMind: true);
    }

    private void OnReleaseAction(ObrNadzorReleaseSessionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ObrNadzorSessionComponent>(args.Performer, out var session))
            return;

        if (!TryComp(session.Console, out ObrNadzorConsoleComponent? console))
            return;

        EndSession((session.Console, console), restoreMind: true);
        args.Handled = true;
    }

    private void OnSessionShutdown(Entity<ObrNadzorSessionComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Ending)
            return;

        if (!TryComp(ent.Comp.Console, out ObrNadzorConsoleComponent? console))
            return;

        EndSession((ent.Comp.Console, console), restoreMind: true, fromSession: true);
    }

    private void OnSessionTerminating(Entity<ObrNadzorSessionComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Ending)
            return;

        if (!TryComp(ent.Comp.Console, out ObrNadzorConsoleComponent? console))
            return;

        EndSession((ent.Comp.Console, console), restoreMind: true, fromSession: true);
    }

    private void TryStartTakeover(Entity<ObrNadzorConsoleComponent> ent, EntityUid actor)
    {
        if (ent.Comp.SessionBrain != null)
        {
            _popup.PopupEntity(Loc.GetString("obr-nadzor-session-active"), actor, actor);
            return;
        }

        if (ent.Comp.Opener != actor || ent.Comp.RemoteEntity == null)
        {
            _popup.PopupEntity(Loc.GetString("obr-nadzor-need-observe"), actor, actor);
            return;
        }

        if (!_mind.TryGetMind(actor, out var mindId, out _))
            return;

        if (ent.Comp.TargetStation is not { } station || !Exists(station))
        {
            _popup.PopupEntity(Loc.GetString("obr-nadzor-no-station"), actor, actor);
            return;
        }

        EntityCoordinates spawnCoords;
        if (TryGetEntity(ent.Comp.RemoteEntity, out var eye) && Exists(eye.Value))
            spawnCoords = Transform(eye.Value).Coordinates;
        else if (_station.GetLargestGrid(station) is { } grid)
            spawnCoords = Transform(grid).Coordinates;
        else
            return;

        // Сначала блэкаут и объява, затем перевод mind.
        BeginStationBlackout(station, ent.Comp.BlackoutDuration);
        AnnounceNadzor(station);
        PlayNadzorSound(station, ent.Comp.AnnouncementSound);

        StopObserving(ent, actor, deleteEye: true);

        var core = Spawn(ent.Comp.AiCoreProto, spawnCoords);
        var brain = SpawnInContainerOrDrop(ent.Comp.AiBrainProto, core, StationAiCoreComponent.Container);

        _meta.SetEntityName(brain, Loc.GetString("obr-nadzor-ai-name"));

        var session = EnsureComp<ObrNadzorSessionComponent>(brain);
        session.Console = ent.Owner;
        session.Body = actor;
        session.Core = core;
        Dirty(brain, session);

        EntityUid? releaseAction = null;
        _actions.AddAction(brain, ref releaseAction, _releaseAction);

        ent.Comp.OperatorBody = actor;
        ent.Comp.SessionBrain = brain;
        ent.Comp.SessionCore = core;
        ent.Comp.Opener = null;
        Dirty(ent);

        _mind.TransferTo(mindId, brain, ghostCheckOverride: true);
        RefreshUiState(ent);
    }

    private void EndSession(Entity<ObrNadzorConsoleComponent> ent, bool restoreMind, bool fromSession = false)
    {
        var brain = ent.Comp.SessionBrain;
        var core = ent.Comp.SessionCore;
        var body = ent.Comp.OperatorBody;

        if (brain != null && TryComp(brain.Value, out ObrNadzorSessionComponent? session))
            session.Ending = true;

        if (restoreMind && brain != null && body != null && Exists(body.Value) && Exists(brain.Value))
        {
            if (_mind.TryGetMind(brain.Value, out var mindId, out _))
                _mind.TransferTo(mindId, body.Value, ghostCheckOverride: true);
        }

        ent.Comp.SessionBrain = null;
        ent.Comp.SessionCore = null;
        ent.Comp.OperatorBody = null;
        Dirty(ent);

        if (!fromSession && brain != null && Exists(brain.Value))
            RemCompDeferred<ObrNadzorSessionComponent>(brain.Value);

        if (core != null && Exists(core.Value))
            QueueDel(core.Value);
        else if (brain != null && Exists(brain.Value))
            QueueDel(brain.Value);

        RefreshUiState(ent);
    }

    private void AnnounceNadzor(EntityUid station)
    {
        _chat.DispatchStationAnnouncement(
            station,
            Loc.GetString("obr-nadzor-announcement"),
            sender: Loc.GetString("obr-nadzor-announcement-sender"),
            playDefault: false,
            playTts: false,
            playDefaultSound: false,
            colorOverride: Color.FromHex("#c22532"));
    }

    private void PlayNadzorSound(EntityUid station, SoundSpecifier sound)
    {
        var filter = _station.GetInOwningStation(station);
        _audio.PlayGlobal(sound, filter, true, AudioParams.Default.WithVolume(1f));
    }
}
