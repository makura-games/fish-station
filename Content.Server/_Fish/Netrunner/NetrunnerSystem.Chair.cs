using Content.Shared._Fish.Netrunner;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Components;
using Content.Shared.Power;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared.StationAi;
using Robust.Shared.Player;

namespace Content.Server._Fish.Netrunner;

public sealed partial class NetrunnerSystem
{
    private void OnStrapped(Entity<NetrunnerChairComponent> ent, ref StrappedEvent args)
    {
        if (_userQuery.TryComp(args.Buckle, out var user)
            && _implantQuery.TryComp(user.Implant, out var implant))
            TryConnect(new Entity<NetrunnerImplantComponent>(user.Implant!.Value, implant), ent);
    }

    private void OnUnstrapped(Entity<NetrunnerChairComponent> ent, ref UnstrappedEvent args)
    {
        if (_userQuery.TryComp(args.Buckle, out var user)
            && _implantQuery.TryComp(user.Implant, out var implant) && implant.Chair == ent.Owner)
            EndRemoteSession(new Entity<NetrunnerImplantComponent>(user.Implant!.Value, implant));
    }

    private void OnChairPower(Entity<NetrunnerChairComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            StopChair(ent);
    }

    private void OnChairShutdown(Entity<NetrunnerChairComponent> ent, ref ComponentShutdown args) => StopChair(ent);

    private void OnRemoteEyeShutdown(Entity<NetrunnerRemoteEyeComponent> ent, ref ComponentShutdown args)
    {
        if (_userQuery.TryComp(ent.Comp.User, out var user)
            && _implantQuery.TryComp(user.Implant, out var implant) && implant.RemoteEye == ent.Owner)
            EndRemoteSession((user.Implant!.Value, implant));
    }

    private void StopChair(EntityUid chair)
    {
        var query = EntityQueryEnumerator<NetrunnerImplantComponent>();
        while (query.MoveNext(out var uid, out var implant))
        {
            if (implant.Chair == chair)
                EndRemoteSession(new Entity<NetrunnerImplantComponent>(uid, implant));
        }
    }

    /// <summary>Begins a connection while keeping the player's mind attached to their body.</summary>
    public bool TryConnect(Entity<NetrunnerImplantComponent> implant, Entity<NetrunnerChairComponent> chair)
    {
        if (!CanConnect(implant, chair) || implant.Comp.User is not { } user)
            return false;
        var finished = new NetrunnerConnectDoAfterEvent();
        var connectTime = implant.Comp.DebugMode ? TimeSpan.Zero : chair.Comp.ConnectTime;
        var args = new DoAfterArgs(EntityManager, user, connectTime,
            finished, implant.Owner, target: chair.Owner)
        {
            NeedHand = false,
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = null,
            AttemptFrequency = AttemptFrequency.EveryTick,
            CancelDuplicate = false,
        };
        // Fish-edit: Начальная проверка подключения должна видеть выбранное кресло.
        implant.Comp.Chair = chair;
        if (!_doAfter.TryStartDoAfter(args, out var id))
        {
            implant.Comp.Chair = null;
            return false;
        }
        implant.Comp.Connection = id;
        implant.Comp.NextConnectionCharge = _timing.CurTime + connectTime + chair.Comp.ChargeInterval;
        // Fish-edit: Мгновенное подключение завершаем после сохранения идентификатора DoAfter.
        if (!_doAfter.IsRunning(id))
            OnConnected(implant, ref finished);
        Publish(implant);
        _ui.TryOpenUi(implant.Owner, NetrunnerUiKey.Terminal, user);
        return true;
    }

    /// <summary>Checks the operator, resources and physical chair connection without changing state.</summary>
    public bool CanConnect(Entity<NetrunnerImplantComponent> implant, Entity<NetrunnerChairComponent> chair)
    {
        if (Terminating(chair) || Paused(chair) || chair.Comp.LifeStage >= ComponentLifeStage.Stopping
            || implant.Comp.User is not { } user || !IsOperator(implant, user)
            || implant.Comp.Chair != null || implant.Comp.Pending != null
            || implant.Comp.Ram < chair.Comp.ConnectionCost
            || HasComp<RelayInputMoverComponent>(user) || !HasComp<EyeComponent>(user)
            || _station.GetOwningStation(chair) == null
            || !TryComp<BuckleComponent>(user, out var buckle) || buckle.BuckledTo != chair.Owner
            || !Transform(chair).Anchored || !_power.IsPowered((chair.Owner, null)))
            return false;
        return true;
    }

    private void OnConnectAttempt(Entity<NetrunnerImplantComponent> ent,
        ref DoAfterAttemptEvent<NetrunnerConnectDoAfterEvent> args)
    {
        if (!CanUseChair(ent))
            args.Cancel();
    }

    private void OnConnected(Entity<NetrunnerImplantComponent> ent, ref NetrunnerConnectDoAfterEvent args)
    {
        if (args.Handled || ent.Comp.Connection != args.DoAfter.Id)
            return;
        args.Handled = true;
        ent.Comp.Connection = null;
        if (args.Cancelled || !CanUseChair(ent) || ent.Comp.Chair is not { } chair)
        {
            EndRemoteSession(ent);
            return;
        }
        var config = Comp<NetrunnerChairComponent>(chair);
        if (ent.Comp.Ram < config.ConnectionCost)
        {
            EndRemoteSession(ent);
            return;
        }
        if (!StartRemoteView(ent))
        {
            EndRemoteSession(ent);
            return;
        }
        if (!ent.Comp.DebugMode)
            ent.Comp.Ram = MathF.Max(0f, ent.Comp.Ram - config.ConnectionCost);
        ent.Comp.RecoverAt = _timing.CurTime + ent.Comp.RecoveryDelay;
        AddTrace(ent, config.ConnectionTrace);
        ent.Comp.NextConnectionCharge = _timing.CurTime + config.ChargeInterval;
        ent.Comp.NextLinkAt = _timing.CurTime + config.LinkInterval;
        Publish(ent);
    }

    private bool CanUseChair(Entity<NetrunnerImplantComponent> ent)
    {
        return ent.Comp.User is { } user && IsOperator(ent, user)
            && ent.Comp.Chair is { } chair && !Terminating(chair) && !Paused(chair)
            && TryComp<NetrunnerChairComponent>(chair, out var config)
            && config.LifeStage < ComponentLifeStage.Stopping && Transform(chair).Anchored
            && _power.IsPowered((chair, null))
            && _station.GetOwningStation(chair) is { } station
            && TryComp<BuckleComponent>(user, out var buckle) && buckle.BuckledTo == chair
            && (ent.Comp.RemoteEye is not { } remote || !Terminating(remote)
                && TryComp<NetrunnerRemoteEyeComponent>(remote, out var viewer) && viewer.Station == station
                && TryComp<RelayInputMoverComponent>(user, out var relay) && relay.RelayEntity == remote);
    }

    private bool CanUseCamera(Entity<NetrunnerImplantComponent> ent, EntityUid camera)
    {
        return CanUseChair(ent) && !Terminating(camera) && !Paused(camera)
            && TryComp<SurveillanceCameraComponent>(camera, out var surveillance) && surveillance.Active
            && !HasComp<NetrunnerDisruptionComponent>(camera) && HasComp<EyeComponent>(camera)
            && TryComp<StationAiVisionComponent>(camera, out var vision) && vision.Enabled
            && (!vision.NeedsAnchoring || Transform(camera).Anchored)
            && _power.IsPowered((camera, null))
            && _station.GetOwningStation(ent.Comp.Chair) is { } station
            && _station.GetOwningStation(camera) == station;
    }

    private bool StartRemoteView(Entity<NetrunnerImplantComponent> ent)
    {
        if (ent.Comp.User is not { } user || ent.Comp.Chair is not { } chair
            || _station.GetOwningStation(chair) is not { } station
            || !TryComp<EyeComponent>(user, out var bodyEye))
            return false;
        EntityUid? closest = null;
        var minDistance = float.PositiveInfinity;
        var position = _transform.GetMapCoordinates(chair);
        var query = EntityQueryEnumerator<SurveillanceCameraComponent>();
        while (query.MoveNext(out var camera, out _))
        {
            if (!CanUseCamera(ent, camera))
                continue;
            var cameraPosition = _transform.GetMapCoordinates(camera);
            if (cameraPosition.MapId != position.MapId)
                continue;
            var distance = (cameraPosition.Position - position.Position).LengthSquared();
            if (distance >= minDistance)
                continue;
            minDistance = distance;
            closest = camera;
        }
        var remote = Spawn("FishNetrunnerRemoteEye", Transform(closest ?? chair).Coordinates);
        var viewer = Comp<NetrunnerRemoteEyeComponent>(remote);
        viewer.User = user;
        viewer.Station = station;
        Dirty(remote, viewer);
        ent.Comp.RemoteEye = remote;
        ent.Comp.Camera = closest;
        ent.Comp.OriginalEyeTarget = bodyEye.Target;
        ent.Comp.OriginalDrawFov = bodyEye.DrawFov;
        // Fish-edit: Actor исчезает до события отсоединения, поэтому сессия нужна для уборки подписки.
        _remoteSessions[ent.Owner] = Comp<ActorComponent>(user).PlayerSession;
        _eye.SetTarget(user, remote, bodyEye);
        _eye.SetDrawFov(user, false, bodyEye);
        _eye.RefreshVisibilityMask(remote);
        _eye.RefreshVisibilityMask(user);
        _actions.AddAction(user, ref ent.Comp.JumpToChairAction, "FishActionNetrunnerJumpToChair", ent);
        _mover.SetRelay(user, remote);
        _mover.ResetCamera(user);
        return true;
    }

    private void OnJumpToChair(Entity<NetrunnerImplantComponent> ent, ref NetrunnerJumpToChairActionEvent args)
    {
        if (args.Handled)
            return;
        if (TryJumpToChair(ent, args.Performer))
            args.Handled = true;
    }

    /// <summary>Moves the mobile viewer back to the operator's connected netrunner chair.</summary>
    public bool TryJumpToChair(Entity<NetrunnerImplantComponent> ent, EntityUid user)
    {
        if (!CanJumpToChair(ent, user) || ent.Comp.Chair is not { } chair || ent.Comp.RemoteEye is not { } remote)
            return false;
        _transform.SetCoordinates(remote, Transform(chair).Coordinates);
        _transform.AttachToGridOrMap(remote);
        _mover.ResetCamera(user);
        ent.Comp.Camera = null;
        Publish(ent);
        return true;
    }

    /// <summary>Checks whether the operator can jump the remote eye back to their connected chair.</summary>
    public bool CanJumpToChair(Entity<NetrunnerImplantComponent> ent, EntityUid user)
    {
        return IsOperator(ent, user) && CanUseChair(ent)
            && ent.Comp.Chair != null && ent.Comp.RemoteEye is { } remote && !Terminating(remote);
    }

    private void OnSelectCamera(Entity<NetrunnerImplantComponent> ent, ref NetrunnerSelectCameraMessage args)
    {
        if (TryGetEntity(args.Camera, out var camera))
            TrySelectCamera(ent, args.Actor, camera.Value);
    }

    /// <summary>Moves the mobile viewer to a validated camera on the chair's station.</summary>
    public bool TrySelectCamera(Entity<NetrunnerImplantComponent> ent, EntityUid user, EntityUid camera)
    {
        if (!CanSelectCamera(ent, user, camera))
            return false;
        CancelHack(ent);
        var remote = ent.Comp.RemoteEye!.Value;
        _transform.SetCoordinates(remote, Transform(camera).Coordinates);
        _transform.AttachToGridOrMap(remote);
        _mover.ResetCamera(user);
        ent.Comp.Camera = camera;
        ent.Comp.NextCameraSwitch = _timing.CurTime + (ent.Comp.DebugMode ? TimeSpan.Zero : TimeSpan.FromSeconds(2));
        Publish(ent);
        return true;
    }

    /// <summary>Checks authorization, connection completion and camera availability.</summary>
    public bool CanSelectCamera(Entity<NetrunnerImplantComponent> ent, EntityUid user, EntityUid camera)
    {
        return IsOperator(ent, user) && ent.Comp.Connection == null
            && ent.Comp.RemoteEye is { } remote && !Terminating(remote)
            && _timing.CurTime >= ent.Comp.NextCameraSwitch && CanUseCamera(ent, camera);
    }

    private void OnDisconnect(Entity<NetrunnerImplantComponent> ent, ref NetrunnerDisconnectMessage args)
    {
        if (IsOperator(ent, args.Actor))
            EndRemoteSession(ent);
    }

    private void OnRefresh(Entity<NetrunnerImplantComponent> ent, ref NetrunnerRefreshMessage args)
    {
        if (!IsOperator(ent, args.Actor))
            return;
        if (ent.Comp.Chair == null && TryComp<BuckleComponent>(args.Actor, out var buckle)
            && buckle.BuckledTo is { } chair && TryComp<NetrunnerChairComponent>(chair, out var config))
            TryConnect(ent, (chair, config));
        Publish(ent);
    }

    private void StopRemoteView(Entity<NetrunnerImplantComponent> ent)
    {
        var remote = ent.Comp.RemoteEye;
        ent.Comp.RemoteEye = null;
        ent.Comp.Camera = null;
        _remoteSessions.Remove(ent.Owner, out var session);
        if (remote is not { } active)
            return;
        if (ent.Comp.User is { } user && !Terminating(user))
        {
            if (ent.Comp.JumpToChairAction is { } jumpAction)
            {
                _actions.RemoveAction(user, jumpAction);
                ent.Comp.JumpToChairAction = null;
            }
            if (TryComp<RelayInputMoverComponent>(user, out var relay) && relay.RelayEntity == active)
                _mover.RemoveRelay(user);
            if (TryComp<EyeComponent>(user, out var bodyEye) && bodyEye.Target == active)
            {
                var original = ent.Comp.OriginalEyeTarget;
                _eye.SetTarget(user, original is { } target && !Terminating(target) ? target : null, bodyEye);
                _eye.SetDrawFov(user, ent.Comp.OriginalDrawFov, bodyEye);
            }
            _eye.RefreshVisibilityMask(user);
        }
        if (session != null)
            _viewSubscriber.RemoveViewSubscriber(active, session);
        if (!Terminating(active))
            QueueDel(active);
    }

    /// <summary>Revokes the view subscription and cancels outstanding work on every exit path.</summary>
    public void EndRemoteSession(Entity<NetrunnerImplantComponent> ent)
    {
        var connection = ent.Comp.Connection;
        var hadSession = connection != null || ent.Comp.Chair != null || ent.Comp.RemoteEye != null;
        ent.Comp.Connection = null;
        ent.Comp.Chair = null;
        StopRemoteView(ent);
        _doAfter.Cancel(connection);
        CancelHack(ent);
        if (hadSession)
            ent.Comp.RecoverAt = _timing.CurTime + ent.Comp.RecoveryDelay;
        Publish(ent);
    }

    private void Publish(Entity<NetrunnerImplantComponent> ent)
    {
        if (ent.Comp.User is not { } user || Terminating(user) || Terminating(ent)
            || !_userQuery.TryComp(user, out var state) || state.Implant != ent.Owner
            || state.LifeStage >= ComponentLifeStage.Stopping)
            return;
        state.Ram = MathF.Max(0f, ent.Comp.Ram);
        state.MaxRam = ent.Comp.MaxRam;
        state.Trace = ent.Comp.Trace;
        state.ReadyAt = ent.Comp.ReadyAt;
        state.Camera = ent.Comp.Camera;
        state.RemoteEye = ent.Comp.RemoteEye;
        state.Chair = ent.Comp.Chair;
        Dirty(user, state);
        var reserved = ent.Comp.Pending == null ? 0 : ProtoMan.Index(ent.Comp.Pending.Ability).RamCost;
        var snapshot = new NetrunnerUiState
        {
            Ram = MathF.Max(0f, ent.Comp.Ram - reserved),
            MaxRam = ent.Comp.MaxRam,
            Trace = ent.Comp.Trace,
            NeuralLinks = ent.Comp.NeuralLinks,
            UnlockedCount = ent.Comp.UnlockedAbilities.Count,
            DebugMode = ent.Comp.DebugMode,
            VisorEquipped = GetWornVisor(ent) != null,
            Cooldown = Math.Max(0, (float)(state.ReadyAt - _timing.CurTime).TotalSeconds),
            Connected = ent.Comp.Chair != null && ent.Comp.Connection == null,
            Connecting = ent.Comp.Connection != null,
            CastRemaining = ent.Comp.Pending is { } pending
                ? Math.Max(0, (float)(pending.FinishesAt - _timing.CurTime).TotalSeconds) : 0,
            Journal = new List<NetrunnerLogEntry>(ent.Comp.Journal),
        };
        PublishBodyTemperature(user, snapshot);
        if (snapshot.Connected && _ui.IsUiOpen(ent.Owner, NetrunnerUiKey.Terminal))
        {
            var query = EntityQueryEnumerator<SurveillanceCameraComponent>();
            while (query.MoveNext(out var camera, out var surveillance))
            {
                if (!CanUseCamera(ent, camera))
                    continue;
                snapshot.Cameras.Add(new NetrunnerCameraEntry(GetNetEntity(camera),
                    surveillance.UseEntityNameAsCameraId ? Name(camera) : surveillance.CameraId));
            }
            snapshot.Cameras.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
        }
        _ui.SetUiState(ent.Owner, NetrunnerUiKey.Terminal, snapshot);
        PublishShop(ent);
    }
}
