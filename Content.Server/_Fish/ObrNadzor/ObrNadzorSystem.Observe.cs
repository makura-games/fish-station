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
        if (ent.Comp.RemoteEntity != null)
        {
            _popup.PopupEntity(Loc.GetString("obr-nadzor-session-active"), args.Actor, args.Actor);
            return;
        }

        var beacon = GetEntity(args.Beacon.NetEnt);
        if (!TryValidateBeacon(ent, beacon, out var station))
            return;

        // Сбросить выбор на другой / этой консоли у того же актёра.
        if (TryGetConsoleForOpener(args.Actor, out var previous) && previous.Owner != ent.Owner)
            ClearTargeting(previous);

        CloseOtherUsers(ent, args.Actor);

        ent.Comp.Opener = args.Actor;
        ent.Comp.TargetBeacon = beacon;
        ent.Comp.TargetStation = station;
        Dirty(ent);
        RefreshUiState(ent);
    }

    private void OnStopObserveMsg(Entity<ObrNadzorConsoleComponent> ent, ref ObrNadzorStopObserveBuiMsg args)
    {
        if (ent.Comp.Opener != args.Actor)
            return;

        // Сессию глаза снимает только Release — StopObserve чистит только выбор маяка.
        if (ent.Comp.RemoteEntity != null)
            return;

        ClearTargeting(ent);
        RefreshUiState(ent);
    }

    private bool TryGetConsoleForOpener(EntityUid actor, out Entity<ObrNadzorConsoleComponent> console)
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

    private void ClearTargeting(Entity<ObrNadzorConsoleComponent> ent)
    {
        ent.Comp.Opener = null;
        ent.Comp.TargetBeacon = null;
        ent.Comp.TargetStation = null;
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
}
