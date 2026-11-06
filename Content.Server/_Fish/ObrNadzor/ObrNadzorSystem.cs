using Content.Server.Actions;
using Content.Server.Chat.Systems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Fish.ObrNadzor;
using Content.Shared.Access.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mind;
using Content.Shared.Movement.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
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
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private MetaDataSystem _meta = default!;

    private static readonly EntProtoId _nanoStation = "StandardNanotrasenStation";
    private static readonly EntProtoId _stopObserveAction = "ActionFishObrNadzorStopObserve";
    private static readonly EntProtoId _releaseAction = "ActionFishObrNadzorRelease";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ObrNadzorConsoleComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<ObrNadzorConsoleComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);

        Subs.BuiEvents<ObrNadzorConsoleComponent>(ObrNadzorUiKey.Key, subs =>
        {
            subs.Event<ObrNadzorBeaconChosenBuiMsg>(OnBeaconChosen);
            subs.Event<ObrNadzorTakeoverBuiMsg>(OnTakeoverMsg);
            subs.Event<ObrNadzorStopObserveBuiMsg>(OnStopObserveMsg);
            subs.Event<ObrNadzorReleaseSessionBuiMsg>(OnReleaseMsg);
        });

        SubscribeLocalEvent<ObrNadzorStopObserveEvent>(OnStopObserveAction);
        SubscribeLocalEvent<ObrNadzorReleaseSessionEvent>(OnReleaseAction);

        SubscribeLocalEvent<ObrNadzorSessionComponent, ComponentShutdown>(OnSessionShutdown);
        SubscribeLocalEvent<ObrNadzorSessionComponent, EntityTerminatingEvent>(OnSessionTerminating);
    }

    private void OnUiOpenAttempt(Entity<ObrNadzorConsoleComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!_access.IsAllowed(args.User, ent))
        {
            args.Cancel();
            return;
        }

        if (ent.Comp.Opener != null && ent.Comp.Opener != args.User && ent.Comp.SessionBrain == null)
        {
            _popup.PopupEntity(Loc.GetString("obr-nadzor-console-occupied"), args.User, args.User);
            args.Cancel();
        }
    }

    private void OnBeforeUiOpen(Entity<ObrNadzorConsoleComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        RefreshUiState(ent);
    }

    private void RefreshUiState(Entity<ObrNadzorConsoleComponent> ent)
    {
        var stations = _station.GetStations();
        var result = new Dictionary<int, ObrNadzorStationBeacons>();

        foreach (var station in stations)
        {
            if (!TryComp(station, out MetaDataComponent? meta) || meta.EntityPrototype == null)
                continue;

            if (meta.EntityPrototype.ID != _nanoStation)
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
            Observing = ent.Comp.RemoteEntity != null && ent.Comp.SessionBrain == null,
            SessionActive = ent.Comp.SessionBrain != null,
        });
    }

    private void CloseOtherUsers(EntityUid console, EntityUid actor)
    {
        if (!TryComp<UserInterfaceComponent>(console, out var bound))
            return;

        foreach (var uids in bound.Actors.Values)
        {
            foreach (var uid in uids)
            {
                if (uid == actor)
                    continue;

                _ui.CloseUi((console, bound), ObrNadzorUiKey.Key, uid);
            }
        }
    }
}
