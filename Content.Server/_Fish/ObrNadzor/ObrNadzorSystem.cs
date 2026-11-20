using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Actions;
using Content.Server.Chat.Systems;
using Content.Server.Power.EntitySystems;
using Content.Shared._Fish.ObrNadzor;
using Content.Shared.Access.Systems;
using Content.Shared.Eye;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Movement.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Content.Shared.GameTicking;
using Content.Shared.Station;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.ObrNadzor;

public sealed partial class ObrNadzorSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private ApcSystem _apc = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly EntProtoId ReleaseAction = "ActionFishObrNadzorRelease";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ObrNadzorConsoleComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<ObrNadzorConsoleComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<ObrNadzorConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
        SubscribeLocalEvent<ObrNadzorConsoleComponent, EntityTerminatingEvent>(OnConsoleTerminating);

        Subs.BuiEvents<ObrNadzorConsoleComponent>(ObrNadzorUiKey.Key, subs =>
        {
            subs.Event<ObrNadzorBeaconChosenBuiMsg>(OnBeaconChosen);
            subs.Event<ObrNadzorTakeoverBuiMsg>(OnTakeoverMsg);
            subs.Event<ObrNadzorStopObserveBuiMsg>(OnStopObserveMsg);
            subs.Event<ObrNadzorReleaseSessionBuiMsg>(OnReleaseMsg);
        });

        SubscribeLocalEvent<ObrNadzorReleaseSessionEvent>(OnReleaseAction);

        SubscribeLocalEvent<ObrNadzorSessionComponent, ComponentShutdown>(OnSessionShutdown);
        SubscribeLocalEvent<ObrNadzorSessionComponent, EntityTerminatingEvent>(OnSessionTerminating);

        SubscribeLocalEvent<ObrNadzorEyeComponent, EntityTerminatingEvent>(OnEyeTerminating);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        CancelPendingBlackouts();
    }

    private void OnUiOpenAttempt(Entity<ObrNadzorConsoleComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!_access.IsAllowed(args.User, ent))
        {
            args.Cancel();
            return;
        }

        // Занято только на этапе выбора маяка (ещё без сессии глаза).
        if (ent.Comp.Opener != null && ent.Comp.Opener != args.User && ent.Comp.RemoteEntity == null)
        {
            _popup.PopupEntity(Loc.GetString("obr-nadzor-console-occupied"), args.User, args.User);
            args.Cancel();
        }
    }

    private void OnBeforeUiOpen(Entity<ObrNadzorConsoleComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        RefreshUiState(ent);
    }

    private void OnConsoleShutdown(Entity<ObrNadzorConsoleComponent> ent, ref ComponentShutdown args)
    {
        ForceCleanupConsole(ent);
    }

    private void OnConsoleTerminating(Entity<ObrNadzorConsoleComponent> ent, ref EntityTerminatingEvent args)
    {
        ForceCleanupConsole(ent);
    }

    private void ForceCleanupConsole(Entity<ObrNadzorConsoleComponent> ent)
    {
        if (ent.Comp.RemoteEntity != null || ent.Comp.OperatorBody != null)
            EndSession(ent);
        else if (ent.Comp.Opener != null)
            ClearTargeting(ent);
    }

    private void RefreshUiState(Entity<ObrNadzorConsoleComponent> ent)
    {
        var stations = _station.GetStations();
        var result = new Dictionary<int, ObrNadzorStationBeacons>();

        foreach (var station in stations)
        {
            if (!TryComp(station, out MetaDataComponent? meta) || meta.EntityPrototype == null)
                continue;

            if (!ent.Comp.StationWhitelist.Contains(meta.EntityPrototype.ID))
                continue;

            if (_station.GetLargestGrid(station) is not { } grid)
                continue;

            if (!TryComp(grid, out NavMapComponent? navMap))
                continue;

            result.Add(station.Id, new ObrNadzorStationBeacons
            {
                Name = meta.EntityName,
                StationId = station.Id,
                Beacons = [.. navMap.Beacons.Values],
            });
        }

        _ui.SetUiState(ent.Owner, ObrNadzorUiKey.Key, new ObrNadzorBuiState
        {
            Stations = result,
            Observing = ent.Comp.Opener != null && ent.Comp.RemoteEntity == null,
            SessionActive = ent.Comp.RemoteEntity != null,
        });
    }

    private void CloseOtherUsers(EntityUid console, EntityUid actor)
    {
        // Снимок: CloseUi мутирует Actors во время перебора.
        var actors = _ui.GetActors(console, ObrNadzorUiKey.Key).ToArray();
        foreach (var uid in actors)
        {
            if (uid == actor)
                continue;

            _ui.CloseUi(console, ObrNadzorUiKey.Key, uid);
        }
    }

    private bool TryValidateBeacon(Entity<ObrNadzorConsoleComponent> ent, EntityUid beacon, [NotNullWhen(true)] out EntityUid? station)
    {
        station = null;

        if (!Exists(beacon) || !HasComp<NavMapBeaconComponent>(beacon))
            return false;

        station = _station.GetOwningStation(beacon);
        if (station == null || !Exists(station.Value))
            return false;

        if (!TryComp(station.Value, out MetaDataComponent? meta) || meta.EntityPrototype == null)
            return false;

        return ent.Comp.StationWhitelist.Contains(meta.EntityPrototype.ID);
    }
}
