using Content.Shared._Fish.ObrNadzor;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Server.GameObjects;

namespace Content.Server._Fish.ObrNadzor;

public sealed partial class ObrNadzorSystem
{
    private void OnBeaconChosen(Entity<ObrNadzorConsoleComponent> ent, ref ObrNadzorBeaconChosenBuiMsg args)
    {
        if (ent.Comp.SessionBrain != null)
        {
            _popup.PopupEntity(Loc.GetString("obr-nadzor-session-active"), args.Actor, args.Actor);
            return;
        }

        // Сбросить наблюдение с любой другой консоли / этой же, прежде чем стартовать заново.
        if (TryGetConsoleForObserver(args.Actor, out var previous))
            StopObserving(previous, args.Actor, deleteEye: true);
        else
            StopObserving(ent, args.Actor, deleteEye: true);

        CloseOtherUsers(ent, args.Actor);
        ent.Comp.Opener = args.Actor;

        var beacon = GetEntity(args.Beacon.NetEnt);
        if (!Exists(beacon))
            return;

        var station = _station.GetOwningStation(beacon);
        ent.Comp.TargetStation = station;

        var eye = SpawnAtPosition(ent.Comp.RemoteEntityProto, Transform(beacon).Coordinates);
        ent.Comp.RemoteEntity = GetNetEntity(eye);

        EnsureComp<VisibilityComponent>(eye);

        AddVirtualItems(args.Actor, ent);
        SetEye(args.Actor, eye);
        AddObserveActions(args.Actor, ent);
        Dirty(ent);
        RefreshUiState(ent);
    }

    private void OnStopObserveMsg(Entity<ObrNadzorConsoleComponent> ent, ref ObrNadzorStopObserveBuiMsg args)
    {
        StopObserving(ent, args.Actor, deleteEye: true);
        RefreshUiState(ent);
    }

    private void OnStopObserveAction(ObrNadzorStopObserveEvent args)
    {
        if (args.Handled)
            return;

        if (!TryGetConsoleForObserver(args.Performer, out var console))
            return;

        StopObserving(console, args.Performer, deleteEye: true);
        RefreshUiState(console);
        args.Handled = true;
    }

    private bool TryGetConsoleForObserver(EntityUid actor, out Entity<ObrNadzorConsoleComponent> console)
    {
        var query = EntityQueryEnumerator<ObrNadzorConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Opener != actor)
                continue;

            console = (uid, comp);
            return true;
        }

        console = default;
        return false;
    }

    private void StopObserving(Entity<ObrNadzorConsoleComponent> ent, EntityUid actor, bool deleteEye)
    {
        if (ent.Comp.Opener == actor)
            ent.Comp.Opener = null;

        RemoveObserveActions(actor);
        RemoveEye(actor);
        _virtualItem.DeleteInHandsMatching(actor, ent.Owner);

        if (deleteEye && ent.Comp.RemoteEntity != null && TryGetEntity(ent.Comp.RemoteEntity, out var eye))
        {
            QueueDel(eye.Value);
            ent.Comp.RemoteEntity = null;
        }

        Dirty(ent);
    }

    private void SetEye(EntityUid uid, EntityUid eye)
    {
        if (!TryComp(uid, out EyeComponent? eyeComp))
            return;

        var overlay = EnsureComp<StationAiOverlayComponent>(uid);
        overlay.AllowCrossGrid = true;
        _eye.RefreshVisibilityMask(uid);
        _eye.SetTarget(uid, eye, eyeComp);
        _eye.SetDrawFov(uid, false);
        _mover.SetRelay(uid, eye);
    }

    private void RemoveEye(EntityUid uid)
    {
        if (!TryComp(uid, out EyeComponent? eyeComp))
            return;

        RemCompDeferred<RelayInputMoverComponent>(uid);
        RemCompDeferred<StationAiOverlayComponent>(uid);
        _eye.SetTarget(uid, null, eyeComp);
        _eye.SetDrawFov(uid, true, eyeComp);
    }

    private void AddVirtualItems(EntityUid uid, EntityUid console)
    {
        if (!TryComp<HandsComponent>(uid, out var hands))
            return;

        foreach (var hand in _hands.EnumerateHands((uid, hands)))
        {
            var heldItem = _hands.GetHeldItem((uid, hands), hand);
            if (HasComp<UnremoveableComponent>(heldItem))
                continue;

            _hands.DoDrop(uid, hand);
        }

        if (_virtualItem.TrySpawnVirtualItemInHand(console, uid, out var virtItem1))
            EnsureComp<UnremoveableComponent>(virtItem1.Value);

        if (_virtualItem.TrySpawnVirtualItemInHand(console, uid, out var virtItem2))
            EnsureComp<UnremoveableComponent>(virtItem2.Value);
    }

    private void AddObserveActions(EntityUid actor, Entity<ObrNadzorConsoleComponent> console)
    {
        // храним action entity uid на консоли через temporary components на акторе не обязательно —
        // AddAction с ref на поля консоли неудобен; используем Ensure на акторе через marker.
        var marker = EnsureComp<ObrNadzorObserverComponent>(actor);
        marker.Console = console.Owner;
        _actions.AddAction(actor, ref marker.StopObserveAction, _stopObserveAction);
        Dirty(actor, marker);
    }

    private void RemoveObserveActions(EntityUid actor)
    {
        if (!TryComp<ObrNadzorObserverComponent>(actor, out var marker))
            return;

        _actions.RemoveAction(actor, marker.StopObserveAction);
        RemCompDeferred<ObrNadzorObserverComponent>(actor);
    }
}
