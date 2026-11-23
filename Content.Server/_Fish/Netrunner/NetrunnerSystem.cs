using Content.Server.Administration.Logs;
using Content.Server.Pinpointer;
using Content.Server.Radio.EntitySystems;
using Content.Server.SurveillanceCamera;
using Content.Shared._Fish.Cyberpsychosis;
using Content.Shared._Fish.Netrunner;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Inventory;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Temperature.Components;
using Content.Shared.Temperature.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Radio;
using Content.Shared.Station;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Stunnable;
using Content.Shared.SurveillanceCamera.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Fish.Netrunner;

/// <summary>Authorizes netrunner sessions and owns all program/resource transactions.</summary>
public sealed partial class NetrunnerSystem : EntitySystem
{
    private static readonly ProtoId<RadioChannelPrototype> AlertRadioChannel = "Binary";

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private StationAiVisionSystem _vision = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private SurveillanceCameraSystem _cameras = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private ViewSubscriberSystem _viewSubscriber = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityQuery<NetrunnerImplantComponent> _implantQuery = default!;
    [Dependency] private EntityQuery<NetrunnerUserComponent> _userQuery = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedTemperatureSystem _temperature = default!;
    [Dependency] private SharedCyberpsychosisSystem _cyberpsychosis = default!;
    [Dependency] private EntityQuery<TemperatureComponent> _temperatureQuery = default!;

    private const float RemoteViewVisibilityRange = 12f;

    private readonly Dictionary<EntityUid, TimeSpan> _stationAlerts = new();
    private readonly Dictionary<EntityUid, TimeSpan> _stationHarvestAlerts = new();
    private readonly Dictionary<EntityUid, ICommonSession> _remoteSessions = new();
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        InitializeShop();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
        SubscribeLocalEvent<NetrunnerImplantComponent, ImplantImplantedEvent>(OnImplanted);
        SubscribeLocalEvent<NetrunnerImplantComponent, ImplantRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<NetrunnerImplantComponent, ComponentShutdown>(OnImplantShutdown);
        SubscribeLocalEvent<NetrunnerImplantComponent, EntityUnpausedEvent>(OnUnpaused);
        SubscribeLocalEvent<NetrunnerImplantComponent, NetrunnerTerminalActionEvent>(OnTerminal);
        SubscribeLocalEvent<NetrunnerImplantComponent, NetrunnerJumpToChairActionEvent>(OnJumpToChair);
        SubscribeLocalEvent<NetrunnerImplantComponent, NetrunnerHackActionEvent>(OnHackAction);
        SubscribeLocalEvent<NetrunnerImplantComponent, NetrunnerAreaHackActionEvent>(OnAreaHackAction);
        SubscribeLocalEvent<NetrunnerImplantComponent, NetrunnerHackDoAfterEvent>(OnHackFinished);
        SubscribeLocalEvent<NetrunnerImplantComponent, DoAfterAttemptEvent<NetrunnerHackDoAfterEvent>>(OnHackAttempt);
        SubscribeLocalEvent<NetrunnerImplantComponent, NetrunnerConnectDoAfterEvent>(OnConnected);
        SubscribeLocalEvent<NetrunnerImplantComponent, DoAfterAttemptEvent<NetrunnerConnectDoAfterEvent>>(OnConnectAttempt);
        SubscribeLocalEvent<NetrunnerImplantComponent, BoundUserInterfaceMessageAttempt>(OnUiAttempt);
        SubscribeLocalEvent<NetrunnerUserComponent, PlayerDetachedEvent>(OnDetached);
        SubscribeLocalEvent<NetrunnerUserComponent, MobStateChangedEvent>(OnMobState);
        SubscribeLocalEvent<NetrunnerUserComponent, ComponentShutdown>(OnUserShutdown);
        SubscribeLocalEvent<NetrunnerChairComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<NetrunnerChairComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<NetrunnerChairComponent, PowerChangedEvent>(OnChairPower);
        SubscribeLocalEvent<NetrunnerChairComponent, ComponentShutdown>(OnChairShutdown);
        SubscribeLocalEvent<NetrunnerRemoteEyeComponent, ComponentShutdown>(OnRemoteEyeShutdown);
        SubscribeLocalEvent<TransformComponent, NetrunnerGrantBundleItemEvent>(OnGrantBundleItem);
        Subs.BuiEvents<NetrunnerImplantComponent>(NetrunnerUiKey.Terminal, subs =>
        {
            subs.Event<NetrunnerSelectCameraMessage>(OnSelectCamera);
            subs.Event<NetrunnerDisconnectMessage>(OnDisconnect);
            subs.Event<NetrunnerRefreshMessage>(OnRefresh);
            subs.Event<NetrunnerOpenShopMessage>(OnOpenShop);
        });
        // Fish-start: Отмена активного скрипта по нажатию клавиши использования предмета в мире (E/У).
        CommandBinds.Builder
            .BindBefore(ContentKeyFunctions.ActivateItemInWorld,
                new PointerInputCmdHandler(HandleCancelHackInput, outsidePrediction: true),
                typeof(SharedInteractionSystem))
            .Register<NetrunnerSystem>();
        // Fish-end
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<NetrunnerSystem>();
    }

    private bool HandleCancelHackInput(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        if (session?.AttachedEntity is not { } user
            || !_userQuery.TryComp(user, out var state)
            || !_implantQuery.TryComp(state.Implant, out var implant)
            || implant.Pending == null)
            return false;

        CancelHack((state.Implant.Value, implant));
        return true;
    }

    private void OnImplanted(Entity<NetrunnerImplantComponent> ent, ref ImplantImplantedEvent args)
    {
        if (_userQuery.HasComp(args.Implanted))
        {
            _popup.PopupEntity(Loc.GetString("fish-netrunner-duplicate"), args.Implanted, args.Implanted);
            return;
        }
        if (!ent.Comp.ResourcesInitialized)
        {
            ent.Comp.Ram = ent.Comp.MaxRam;
            ent.Comp.NeuralLinks = ent.Comp.StartingLinks;
            ent.Comp.ResourcesInitialized = true;
        }
        ent.Comp.User = args.Implanted;
        ent.Comp.LastUpdate = _timing.CurTime;
        if (ent.Comp.DebugMode)
        {
            foreach (var id in ent.Comp.Abilities)
                ent.Comp.UnlockedAbilities.Add(id);
            ent.Comp.Ram = ent.Comp.MaxRam;
        }
        var user = AddComp<NetrunnerUserComponent>(args.Implanted);
        user.Implant = ent;
        foreach (var id in ent.Comp.UnlockedAbilities)
            GrantProgram(ent, args.Implanted, id);
        Publish(ent);
    }

    private void OnRoundCleanup(RoundRestartCleanupEvent args)
    {
        var query = EntityQueryEnumerator<NetrunnerImplantComponent>();
        while (query.MoveNext(out var uid, out var implant))
            EndRemoteSession(new Entity<NetrunnerImplantComponent>(uid, implant));
        _stationAlerts.Clear();
        _stationHarvestAlerts.Clear();
        _remoteSessions.Clear();
    }

    private void OnRemoved(Entity<NetrunnerImplantComponent> ent, ref ImplantRemovedEvent args) => RemoveUser(ent);
    private void OnImplantShutdown(Entity<NetrunnerImplantComponent> ent, ref ComponentShutdown args) => RemoveUser(ent);

    private void OnUnpaused(Entity<NetrunnerImplantComponent> ent, ref EntityUnpausedEvent args)
    {
        ent.Comp.LastUpdate += args.PausedTime;
        ent.Comp.RecoverAt += args.PausedTime;
        ent.Comp.ReadyAt += args.PausedTime;
        ent.Comp.LastHackAt += args.PausedTime;
        ent.Comp.NextConnectionCharge += args.PausedTime;
        ent.Comp.NextCameraSwitch += args.PausedTime;
        ent.Comp.NextLinkAt += args.PausedTime;
        foreach (var id in new List<ProtoId<NetrunnerAbilityPrototype>>(ent.Comp.Cooldowns.Keys))
            ent.Comp.Cooldowns[id] += args.PausedTime;
        if (ent.Comp.Pending is { } pending)
            pending.FinishesAt += args.PausedTime;
    }

    private void OnTerminal(Entity<NetrunnerImplantComponent> ent, ref NetrunnerTerminalActionEvent args)
    {
        if (args.Handled || !IsOperator(ent, args.Performer))
            return;
        Publish(ent);
        _ui.TryToggleUi(ent.Owner, NetrunnerUiKey.Terminal, args.Performer);
        args.Handled = true;
    }

    private void OnHackAction(Entity<NetrunnerImplantComponent> ent, ref NetrunnerHackActionEvent args)
    {
        if (args.Handled)
            return;
        if (TryStartHack(ent, args.Performer, args.Target, args.Ability))
            args.Handled = true;
    }

    private void OnAreaHackAction(Entity<NetrunnerImplantComponent> ent, ref NetrunnerAreaHackActionEvent args)
    {
        if (!args.Handled && TryStartAreaHack(ent, args.Performer, args.Target, args.Ability))
            args.Handled = true;
    }

    private void OnHackAttempt(Entity<NetrunnerImplantComponent> ent, ref DoAfterAttemptEvent<NetrunnerHackDoAfterEvent> args)
    {
        if (!CanContinueHack(ent))
            args.Cancel();
    }

    private void OnHackFinished(Entity<NetrunnerImplantComponent> ent, ref NetrunnerHackDoAfterEvent args)
    {
        if (args.Handled || ent.Comp.Pending is not { } pending || pending.DoAfter != args.DoAfter.Id)
            return;
        args.Handled = true;
        if (args.Cancelled || !CanContinueHack(ent))
        {
            FinishHack(ent, false);
            return;
        }
        var ability = ProtoMan.Index(pending.Ability);
        if (pending.AreaCoordinates != null)
        {
            FinishHack(ent, ApplyAreaHack(ent, pending, ability));
            return;
        }
        var apply = new NetrunnerApplyHackEvent(args.User, pending.Device, ability);
        RaiseLocalEvent(pending.Target, ref apply);
        FinishHack(ent, apply.Applied);
    }

    private void OnUiAttempt(Entity<NetrunnerImplantComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (!IsOperator(ent, args.Actor))
            args.Cancel();
    }

    private void OnDetached(Entity<NetrunnerUserComponent> ent, ref PlayerDetachedEvent args) => StopUser(ent);
    private void OnMobState(Entity<NetrunnerUserComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            StopUser(ent);
    }
    private void OnUserShutdown(Entity<NetrunnerUserComponent> ent, ref ComponentShutdown args) => StopUser(ent);

    private void OnGrantBundleItem(EntityUid uid, TransformComponent xform, NetrunnerGrantBundleItemEvent args)
    {
        // Fish-edit: Выдаём дополнительный предмет комплекта отдельно от коробки.
        var item = Spawn(args.Prototype, xform.Coordinates);
        _hands.PickupOrDrop(uid, item);
    }

    /// <summary>Starts one validated program, reserving its RAM until completion.</summary>
    public bool TryStartHack(Entity<NetrunnerImplantComponent> ent, EntityUid user, EntityUid target,
        ProtoId<NetrunnerAbilityPrototype> id)
    {
        if (ProtoMan.TryIndex(id, out var areaAbility) && areaAbility.AreaRadius > 0 && !Terminating(target))
            return TryStartAreaHack(ent, user, Transform(target).Coordinates, id);
        if (!CanStartHack(ent, user, target, id, out var device))
        {
            _popup.PopupEntity(Loc.GetString("fish-netrunner-unavailable"), user, user);
            return false;
        }
        var ability = ProtoMan.Index(id);
        return StartProgram(ent, user, target, device, id, ability);
    }

    private bool StartProgram(Entity<NetrunnerImplantComponent> ent, EntityUid user, EntityUid target,
        EntityUid device, ProtoId<NetrunnerAbilityPrototype> id, NetrunnerAbilityPrototype ability,
        Robust.Shared.Map.EntityCoordinates? areaCoordinates = null, List<EntityUid>? areaTargets = null)
    {
        var origin = ent.Comp.RemoteEye ?? user;
        var castTime = GetCastTime(ent, ability);
        var finished = new NetrunnerHackDoAfterEvent();
        var args = new DoAfterArgs(EntityManager, user, castTime, finished,
            ent.Owner, target: areaCoordinates == null ? target : null)
        {
            NeedHand = false,
            BreakOnMove = false,
            BreakOnDamage = true,
            DistanceThreshold = null,
            AttemptFrequency = AttemptFrequency.EveryTick,
            Hidden = true,
            CancelDuplicate = false,
        };
        var pending = new NetrunnerPendingHack
        {
            Ability = id,
            Target = target,
            Device = device,
            Origin = origin,
            FinishesAt = _timing.CurTime + castTime,
            AreaCoordinates = areaCoordinates,
            AreaTargets = areaTargets,
        };
        // Fish-edit: Начальная проверка DoAfter уже должна видеть контекст взлома.
        ent.Comp.Pending = pending;
        if (!_doAfter.TryStartDoAfter(args, out var doAfter))
        {
            ent.Comp.Pending = null;
            return false;
        }
        pending.DoAfter = doAfter;
        if (ent.Comp.RemoteEye is { } remoteEye && TryComp<NetrunnerRemoteEyeComponent>(remoteEye, out var viewer))
        {
            viewer.CastStart = _timing.CurTime;
            viewer.CastEnd = pending.FinishesAt;
            Dirty(remoteEye, viewer);
        }
        ent.Comp.ReadyAt = _timing.CurTime + GetGlobalCooldown(ent);
        var cooldown = GetCooldown(ent, ability);
        ent.Comp.Cooldowns[id] = _timing.CurTime + cooldown;
        SetProgramUseDelay(ent, ability, cooldown);
        ent.Comp.RecoverAt = _timing.CurTime + castTime + (ent.Comp.DebugMode ? TimeSpan.Zero : ent.Comp.RecoveryDelay);
        AddTrace(ent, ability.TraceCost * 0.2f, ability.Kind == NetrunnerHackKind.NeuralOverload);
        ApplyActivationHeat(ent, user, ability);
        if (!ent.Comp.DebugMode)
            _cyberpsychosis.TryModifyCyberpsychosis(user, 5f, out _);
        // Fish-edit: Мгновенное завершение обрабатываем после фиксации перезарядки и следа.
        if (!_doAfter.IsRunning(doAfter))
            OnHackFinished(ent, ref finished);
        Publish(ent);
        return true;
    }

    /// <summary>Checks authorization, available RAM, view and program-specific restrictions.</summary>
    public bool CanStartHack(Entity<NetrunnerImplantComponent> ent, EntityUid user, EntityUid target,
        ProtoId<NetrunnerAbilityPrototype> id, out EntityUid device)
    {
        device = default;
        if (!CanRunProgram(ent, user, id, out var ability) || !CanTarget(ent, target, ability))
            return false;
        var check = new NetrunnerValidateHackEvent(user, ability);
        RaiseLocalEvent(target, ref check);
        if (!check.Supported || !check.Allowed || check.Device is not { } actual)
            return false;
        device = actual;
        return true;
    }

    private bool CanRunProgram(Entity<NetrunnerImplantComponent> ent, EntityUid user,
        ProtoId<NetrunnerAbilityPrototype> id, out NetrunnerAbilityPrototype ability)
    {
        ability = default!;
        if (!IsOperator(ent, user) || ent.Comp.Pending != null || ent.Comp.Connection != null
            || !ent.Comp.Abilities.Contains(id) || !ent.Comp.UnlockedAbilities.Contains(id)
            || !ProtoMan.TryIndex(id, out var program))
            return false;
        ability = program;
        return ent.Comp.DebugMode || _timing.CurTime >= ent.Comp.ReadyAt
            && ent.Comp.Cooldowns.GetValueOrDefault(id) <= _timing.CurTime
            && ent.Comp.Ram >= 0
            && ent.Comp.Ram - ability.RamCost >= (ability.AllowOverload ? -ent.Comp.MaxOverload : 0);
    }

    private bool CanContinueHack(Entity<NetrunnerImplantComponent> ent)
    {
        if (ent.Comp.Pending is not { } pending || ent.Comp.User is not { } user
            || !IsOperator(ent, user) || pending.AreaCoordinates == null && Terminating(pending.Device)
            || (ent.Comp.RemoteEye ?? user) != pending.Origin)
            return false;
        var ability = ProtoMan.Index(pending.Ability);
        if (pending.AreaCoordinates is { } areaCoordinates)
            return CanTargetArea(ent, areaCoordinates, ability, enforceRange: false);
        if (!CanTarget(ent, pending.Target, ability, enforceRange: false))
            return false;
        var check = new NetrunnerValidateHackEvent(user, ability);
        RaiseLocalEvent(pending.Target, ref check);
        return check.Supported && check.Allowed && check.Device == pending.Device;
    }

    private bool CanTarget(Entity<NetrunnerImplantComponent> ent, EntityUid target, NetrunnerAbilityPrototype ability,
        bool enforceRange = true)
    {
        if (Terminating(target) || Paused(target) || _container.IsEntityInContainer(target)
            || ent.Comp.User is not { } user || target == user)
            return false;
        var origin = ent.Comp.RemoteEye ?? user;
        if (ent.Comp.Chair != null && !CanUseChair(ent))
            return false;
        if (ent.Comp.RemoteEye is { } remote)
        {
            var range = enforceRange ? ability.Range : RemoteViewVisibilityRange;
            if (Terminating(remote) || !_transform.InRange((remote, null), (target, null), range)
                || !TryComp<NetrunnerRemoteEyeComponent>(remote, out var viewer)
                || _station.GetOwningStation(target) != viewer.Station)
                return false;
            var targetXform = Transform(target);
            if (targetXform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid)
                || !TryComp<BroadphaseComponent>(gridUid, out var broadphase))
                return false;
            var tile = _maps.LocalToTile(gridUid, grid, targetXform.Coordinates);
            return _vision.IsNetrunnerAccessible((gridUid, broadphase, grid), tile, viewer.Station, ent.Comp.Chair);
        }
        return _interaction.InRangeUnobstructed((origin, null), (target, null), ability.Range,
            collisionMask: CollisionGroup.Opaque,
            overlapCheck: false);
    }

    private bool IsOperator(Entity<NetrunnerImplantComponent> ent, EntityUid user)
    {
        return !Terminating(ent) && ent.Comp.LifeStage < ComponentLifeStage.Stopping
            && ent.Comp.User == user && !Terminating(user) && HasComp<ActorComponent>(user)
            && _userQuery.TryComp(user, out var state) && state.Implant == ent.Owner
            && state.LifeStage < ComponentLifeStage.Stopping
            && TryComp<SubdermalImplantComponent>(ent, out var implant) && implant.ImplantedEntity == user
            && _actionBlocker.CanInteract(user, null) && _mobState.IsAlive(user);
    }

    private void FinishHack(Entity<NetrunnerImplantComponent> ent, bool success)
    {
        if (ent.Comp.Pending is not { } pending)
            return;
        ent.Comp.Pending = null;
        if (ent.Comp.RemoteEye is { } remoteEye && TryComp<NetrunnerRemoteEyeComponent>(remoteEye, out var viewer))
        {
            viewer.CastStart = TimeSpan.Zero;
            viewer.CastEnd = TimeSpan.Zero;
            Dirty(remoteEye, viewer);
        }
        var ability = ProtoMan.Index(pending.Ability);
        var cost = success ? ability.RamCost
            : ability.Kind == NetrunnerHackKind.NeuralOverload ? ability.RamCost * 0.5f : 0;
        var deficit = ent.Comp.DebugMode ? 0f : MathF.Max(0f, cost - ent.Comp.Ram);
        if (!ent.Comp.DebugMode)
            ent.Comp.Ram = MathF.Max(0f, ent.Comp.Ram - cost);
        ent.Comp.ReadyAt = _timing.CurTime + GetGlobalCooldown(ent);
        if (!success)
        {
            // Fish-edit: После прерывания перезарядка программы ограничивается 4 секундами.
            var interruptedCooldown = TimeSpan.FromSeconds(Math.Min(GetCooldown(ent, ability).TotalSeconds, 4.0));
            var cooldownEnd = _timing.CurTime + interruptedCooldown;
            ent.Comp.Cooldowns[pending.Ability] = cooldownEnd;
            SetProgramCooldown(ent, ability, _timing.CurTime, cooldownEnd);
        }
        ent.Comp.RecoverAt = _timing.CurTime + (ent.Comp.DebugMode ? TimeSpan.Zero : ent.Comp.RecoveryDelay);
        if (ent.Comp.Chair is { } chair && TryComp<NetrunnerChairComponent>(chair, out var cooling))
            ent.Comp.NextLinkAt = _timing.CurTime + cooling.LinkInterval;
        AddTrace(ent, success ? ability.TraceCost * 0.8f : 0, ability.Kind == NetrunnerHackKind.NeuralOverload);
        if (ent.Comp.User is { } user)
        {
            if (success)
            {
                var researchedDevice = pending.AreaTargets is { Count: > 0 } areaTargets
                    ? areaTargets[0]
                    : pending.Device;
                RewardResearch(ent, researchedDevice);
                ApplyOverload(ent, user, deficit, ability.Kind == NetrunnerHackKind.NeuralOverload);
            }
            var name = Deleted(pending.Target) ? string.Empty : Name(pending.Target);
            var entry = new NetrunnerLogEntry(_timing.CurTime, ability.Kind, name, success);
            AppendJournal(ent.Comp.Journal, entry);
            if (ability.Kind is not (NetrunnerHackKind.EraseTrace or NetrunnerHackKind.NeuralOverload))
                RecordDeviceAudits(pending, entry);
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(user):user} used netrunner program {ability.ID} on {ToPrettyString(pending.Target):target}; success: {success}");
            _popup.PopupEntity(Loc.GetString(success ? "fish-netrunner-success" : "fish-netrunner-cancelled"), user, user);
        }
        Publish(ent);
    }

    private void CancelHack(Entity<NetrunnerImplantComponent> ent)
    {
        if (ent.Comp.Pending is not { } pending)
            return;
        _doAfter.Cancel(pending.DoAfter);
        FinishHack(ent, false);
    }

    private void ApplyOverload(Entity<NetrunnerImplantComponent> ent, EntityUid user, float deficit, bool silent = false)
    {
        if (ent.Comp.DebugMode)
            return;
        ent.Comp.BurstCount = _timing.CurTime - ent.Comp.LastHackAt < TimeSpan.FromSeconds(12)
            ? ent.Comp.BurstCount + 1 : 1;
        ent.Comp.LastHackAt = _timing.CurTime;
        var severity = MathF.Max(0, deficit) + Math.Max(0, ent.Comp.BurstCount - 2);
        if (severity <= 0)
            return;
        _bloodstream.TryModifyBleedAmount((user, null), severity * 0.25f);
        if (severity >= 2)
            _stun.TryUpdateParalyzeDuration(user, TimeSpan.FromSeconds(Math.Min(4, severity)));
        AddTrace(ent, severity * 5, silent);
        _popup.PopupEntity(Loc.GetString("fish-netrunner-overloaded"), user, user);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_nextUpdate > _timing.CurTime)
            return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(0.25);
        var query = EntityQueryEnumerator<NetrunnerImplantComponent>();
        while (query.MoveNext(out var uid, out var implant))
        {
            var ent = new Entity<NetrunnerImplantComponent>(uid, implant);
            var elapsed = Math.Max(0, (_timing.CurTime - implant.LastUpdate).TotalSeconds);
            var recoveryTime = Math.Max(0, (_timing.CurTime - Max(implant.LastUpdate, implant.RecoverAt)).TotalSeconds);
            implant.LastUpdate = _timing.CurTime;
            var connected = implant.Connection == null && implant.Chair != null && CanUseChair(ent);
            var cooling = connected ? Comp<NetrunnerChairComponent>(implant.Chair!.Value) : null;
            var traceRate = cooling?.TracePerSecond ?? implant.TracePerSecond;
            if (implant.Pending == null)
                implant.Trace = Math.Max(0, implant.Trace - traceRate * (float)elapsed);
            if (implant.Trace < 25) implant.TraceTier = 0;
            else if (implant.Trace < 55) implant.TraceTier = Math.Min(implant.TraceTier, 1);
            else if (implant.Trace < 85) implant.TraceTier = Math.Min(implant.TraceTier, 2);
            if (implant.User is not { } user)
                continue;
            if (implant.DebugMode)
                implant.Ram = implant.MaxRam;
            if (!IsOperator(ent, user))
            {
                CancelHack(ent);
                EndRemoteSession(ent);
                if (_ui.IsUiOpen(uid, NetrunnerUiKey.Terminal))
                    _ui.CloseUi(uid, NetrunnerUiKey.Terminal);
                if (_ui.IsUiOpen(uid, NetrunnerUiKey.Shop))
                    _ui.CloseUi(uid, NetrunnerUiKey.Shop);
            }
            else if (implant.Connection == null && implant.Pending == null)
            {
                var ramRate = cooling?.RamPerSecond ?? implant.RamPerSecond;
                implant.Ram = Math.Clamp(implant.Ram + ramRate * (float)recoveryTime, 0f, implant.MaxRam);
            }
            if (implant.Chair != null)
            {
                if (!CanUseChair(ent))
                    EndRemoteSession(ent);
                if (implant.Chair is { } chair && _timing.CurTime >= implant.NextConnectionCharge)
                {
                    var config = Comp<NetrunnerChairComponent>(chair);
                    var reserved = implant.Pending == null ? 0 : ProtoMan.Index(implant.Pending.Ability).RamCost;
                    if (implant.Ram - reserved < config.UpkeepCost)
                        EndRemoteSession(ent);
                    else
                    {
                        if (!implant.DebugMode)
                            implant.Ram = MathF.Max(0f, implant.Ram - config.UpkeepCost);
                        AddTrace(ent, config.UpkeepTrace);
                        implant.NextConnectionCharge = _timing.CurTime + config.ChargeInterval;
                    }
                }
            }
            if (cooling != null && implant.Chair != null && implant.Pending == null
                && implant.Trace <= cooling.CompilationTraceLimit && implant.PassiveLinks < cooling.MaxPassiveLinks)
            {
                if (_timing.CurTime >= implant.NextLinkAt)
                {
                    implant.NeuralLinks++;
                    implant.PassiveLinks++;
                    implant.NextLinkAt = _timing.CurTime + cooling.LinkInterval;
                    if (implant.PassiveLinks == 9)
                        NotifyDataHarvest(ent);
                }
            }
            else if (cooling != null)
                implant.NextLinkAt = _timing.CurTime + cooling.LinkInterval;
            if (_timing.CurTime >= implant.NextUiUpdate)
            {
                Publish(ent);
                implant.NextUiUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
            }
        }
    }

    private void AddTrace(Entity<NetrunnerImplantComponent> ent, float amount, bool silent = false)
    {
        if (ent.Comp.DebugMode)
            return;
        ent.Comp.Trace = Math.Clamp(ent.Comp.Trace + amount, 0, 100);
        var tier = ent.Comp.Trace >= 89.9f ? 3 : ent.Comp.Trace >= 59.9f ? 2 : ent.Comp.Trace >= 29.9f ? 1 : 0;
        if (tier > ent.Comp.TraceTier)
        {
            ent.Comp.TraceTier = tier;
            if (!silent)
                NotifyStation(ent, tier >= 3);
        }
    }

    private void NotifyStation(Entity<NetrunnerImplantComponent> ent, bool revealLocation)
    {
        if (ent.Comp.User is not { } user)
            return;
        var source = ent.Comp.Chair ?? user;
        if (_station.GetOwningStation(source) is not { } station)
            return;
        if (!revealLocation && _stationAlerts.GetValueOrDefault(station) > _timing.CurTime)
            return;
        if (!ProtoMan.TryIndex(AlertRadioChannel, out var channel))
            return;
        _stationAlerts[station] = _timing.CurTime + TimeSpan.FromSeconds(30);
        var message = revealLocation
            ? Loc.GetString("fish-netrunner-trace-alert-location",
                ("position", FormattedMessage.RemoveMarkupPermissive(_navMap.GetNearestBeaconString(source))))
            : Loc.GetString("fish-netrunner-trace-alert");
        _radio.SendRadioMessage(station, message, channel, source);
    }

    private void NotifyDataHarvest(Entity<NetrunnerImplantComponent> ent)
    {
        if (ent.Comp.User is not { } user)
            return;
        var source = ent.Comp.Chair ?? user;
        if (_station.GetOwningStation(source) is not { } station)
            return;
        if (_stationHarvestAlerts.GetValueOrDefault(station) > _timing.CurTime)
            return;
        if (!ProtoMan.TryIndex(AlertRadioChannel, out var channel))
            return;
        _stationHarvestAlerts[station] = _timing.CurTime + TimeSpan.FromSeconds(120);
        _radio.SendRadioMessage(station, Loc.GetString("fish-netrunner-harvest-alert"), channel, source);
    }

    private void RemoveUser(Entity<NetrunnerImplantComponent> ent)
    {
        CancelHack(ent);
        EndRemoteSession(ent);
        if (ent.Comp.User is { } user && !Deleted(user))
        {
            foreach (var action in ent.Comp.GrantedActions)
                _actions.RemoveAction(user, action);
            if (_userQuery.TryComp(user, out var state) && state.Implant == ent.Owner)
                RemComp<NetrunnerUserComponent>(user);
            if (!Terminating(ent.Owner))
            {
                _ui.CloseUi(ent.Owner, NetrunnerUiKey.Terminal);
                _ui.CloseUi(ent.Owner, NetrunnerUiKey.Shop);
            }
        }
        ent.Comp.GrantedActions.Clear();
        ent.Comp.User = null;
    }

    private void StopUser(Entity<NetrunnerUserComponent> ent)
    {
        if (_implantQuery.TryComp(ent.Comp.Implant, out var implant))
        {
            var implantEnt = new Entity<NetrunnerImplantComponent>(ent.Comp.Implant!.Value, implant);
            CancelHack(implantEnt);
            EndRemoteSession(implantEnt);
            if (!Terminating(implantEnt))
            {
                _ui.CloseUi(implantEnt.Owner, NetrunnerUiKey.Terminal);
                _ui.CloseUi(implantEnt.Owner, NetrunnerUiKey.Shop);
            }
        }
    }

    private static void AppendJournal(List<NetrunnerLogEntry> journal, NetrunnerLogEntry entry)
    {
        if (journal.Count >= 16)
            journal.RemoveAt(0);
        journal.Add(entry);
    }

    private static TimeSpan Max(TimeSpan first, TimeSpan second) => first > second ? first : second;
}
