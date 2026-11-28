using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.DeviceNetwork;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Fish.Artillery;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Examine;
using Content.Shared.Explosion;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Sprite;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.UserInterface;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Power;
using Content.Shared.Maps;
using Content.Shared.GameTicking;
using Content.Shared.Station.Components;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Server.GameObjects;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using System.Numerics;

namespace Content.Server._Fish.Artillery;

public sealed partial class BluespaceArtillerySystem : SharedBluespaceArtillerySystem
{
    [Dependency] private AudioSystem _audioServer = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedDeviceLinkSystem _deviceLink = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private ShuttleConsoleSystem _shuttleConsole = default!;
    [Dependency] private SharedShuttleSystem _shuttle = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;

    [Dependency] private EntityQuery<AccessReaderComponent> _accessReaderQuery = default!;
    [Dependency] private EntityQuery<PowerConsumerComponent> _powerConsumerQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;
    [Dependency] private EntityQuery<IFFComponent> _iffQuery = default!;

    private float _uiUpdateAccumulator;
    private const float UiUpdateInterval = 1.0f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BluespaceArtilleryComponent, NewLinkEvent>(OnArtilleryNewLink);
        SubscribeLocalEvent<BluespaceArtilleryComponent, PortDisconnectedEvent>(OnArtilleryPortDisconnected);
        SubscribeLocalEvent<BluespaceArtilleryComponent, LinkAttemptEvent>(OnArtilleryLinkAttempt);
        SubscribeLocalEvent<BluespaceArtilleryComponent, MapInitEvent>(OnArtilleryMapInit);
        SubscribeLocalEvent<BluespaceArtilleryComponent, ExaminedEvent>(OnArtilleryExamined);
        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, LinkAttemptEvent>(OnConsoleLinkAttempt);
        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, PortDisconnectedEvent>(OnConsolePortDisconnected);
        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, MapInitEvent>(OnConsoleMapInit);
        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, NewLinkEvent>(OnConsoleNewLink);

        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, BluespaceArtilleryFireMessage>(OnFireMessage);
        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, BluespaceArtillerySetCoordsMessage>(OnSetCoordsMessage);
        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, BluespaceArtillerySetParamsMessage>(OnSetParamsMessage);
        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, BluespaceArtilleryPreviewMessage>(OnPreviewMessage);
        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, BluespaceArtillerySelectTargetStationMessage>(OnSelectTargetStationMessage);
        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, BluespaceArtillerySelectTargetObjectMessage>(OnSelectTargetObjectMessage);
        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, AfterActivatableUIOpenEvent>(OnAfterActivatableUIOpen);

        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
        SubscribeLocalEvent<BluespaceArtilleryComponent, ComponentShutdown>(OnArtilleryShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _uiUpdateAccumulator += frameTime;
        if (_uiUpdateAccumulator < UiUpdateInterval)
            return;

        _uiUpdateAccumulator = 0f;

        var query = EntityQueryEnumerator<BluespaceArtilleryConsoleComponent>();
        while (query.MoveNext(out var consoleUid, out var console))
        {
            if (!_ui.IsUiOpen(consoleUid, BluespaceArtilleryConsoleUiKey.Key))
                continue;

            UpdateUI(consoleUid, console);
        }
    }

    private bool HasAccess(EntityUid consoleUid, EntityUid actor)
    {
        if (_accessReaderQuery.TryComp(consoleUid, out var access) &&
            !_accessReader.IsAllowed(actor, consoleUid, access))
        {
            _popup.PopupEntity(Loc.GetString("bluespace-artillery-no-access"), consoleUid, actor);
            return false;
        }

        return true;
    }

    private bool IsArtilleryPowered(EntityUid artilleryUid)
    {
        if (_powerConsumerQuery.TryComp(artilleryUid, out var powerConsumer))
        {
            return powerConsumer.Net != null &&
                   (powerConsumer.DrawRate <= 0f ||
                    powerConsumer.ReceivedPower >= powerConsumer.DrawRate * 0.99f);
        }

        return _powerReceiver.IsPowered(artilleryUid);
    }

    private void SetArtilleryPowerDraw(EntityUid artilleryUid, float drawRate)
    {
        if (_powerConsumerQuery.TryComp(artilleryUid, out var powerConsumer))
            powerConsumer.DrawRate = drawRate;
    }

    private void OnArtilleryExamined(Entity<BluespaceArtilleryComponent> ent, ref ExaminedEvent args)
    {
        var powered = IsArtilleryPowered(ent.Owner);
        var stateText = Loc.GetString(powered
            ? "power-receiver-component-on-examine-powered"
            : "power-receiver-component-on-examine-unpowered");
        args.PushMarkup(Loc.GetString("power-receiver-component-on-examine-main", ("stateText", stateText)));
    }

    private bool TryGetTargetObjectWorldPosition(EntityUid targetUid, out MapCoordinates mapCoords)
    {
        mapCoords = MapCoordinates.Nullspace;
        if (!Exists(targetUid) || Deleted(targetUid))
            return false;

        if (!_xformQuery.TryComp(targetUid, out var xform) || xform.MapID == MapId.Nullspace)
            return false;

        var localCenter = _physicsQuery.TryComp(targetUid, out var body)
            ? body.LocalCenter
            : Vector2.Zero;
        var worldPos = Vector2.Transform(localCenter, _transform.GetWorldMatrix(xform));
        mapCoords = new MapCoordinates(worldPos, xform.MapID);
        return true;
    }

    private void SetArtilleryVisualState(EntityUid uid, BluespaceArtilleryVisualState state)
    {
        _appearance.SetData(uid, BluespaceArtilleryVisuals.VisualState, state);
    }

    private void OnConsoleMapInit(EntityUid uid, BluespaceArtilleryConsoleComponent comp, ref MapInitEvent args)
    {
        if (!TryComp<DeviceLinkSourceComponent>(uid, out var source))
            return;

        var linkedSinks = _deviceLink.GetLinkedSinks((uid, source), comp.LinkingPort);
        foreach (var sink in linkedSinks)
        {
            if (TryComp<BluespaceArtilleryComponent>(sink, out var artillery))
            {
                artillery.LinkedConsole = uid;
                comp.LinkedArtillery = sink;
                UpdateUI(uid, comp);
                break;
            }
        }
    }

    private void OnArtilleryMapInit(EntityUid uid, BluespaceArtilleryComponent comp, ref MapInitEvent args)
    {
        SetArtilleryPowerDraw(uid, comp.IdlePowerDraw);

        if (!TryComp<DeviceLinkSinkComponent>(uid, out var sink))
            return;

        var query = EntityQueryEnumerator<DeviceLinkSourceComponent>();
        while (query.MoveNext(out var sourceUid, out var sourceComp))
        {
            var linkedSinks = _deviceLink.GetLinkedSinks((sourceUid, sourceComp), comp.LinkingPort);
            if (!linkedSinks.Contains(uid))
                continue;

            if (TryComp<BluespaceArtilleryConsoleComponent>(sourceUid, out var console))
            {
                console.LinkedArtillery = uid;
                comp.LinkedConsole = sourceUid;
                UpdateUI(sourceUid, console);
                break;
            }
        }
    }

    private void InitTargetStation(EntityUid uid, BluespaceArtilleryConsoleComponent comp)
    {
        var ownMapId = Transform(uid).MapID;

        foreach (var station in _station.GetStations())
        {
            if (TryComp<StationDataComponent>(station, out var data))
            {
                var mainGrid = _station.GetLargestGrid((station, data));
                if (mainGrid != null)
                {
                    var xform = Transform(mainGrid.Value);
                    if (xform.MapID != ownMapId || comp.TargetMapId == null)
                    {
                        comp.TargetStation = station;
                        comp.TargetMapId = xform.MapID;
                        if (comp.TargetCoordinates.Equals(ArtilleryVector2.Zero))
                        {
                            var pos = _transform.GetWorldPosition(mainGrid.Value);
                            comp.TargetCoordinates = new ArtilleryVector2(pos.X, pos.Y);
                        }
                        if (xform.MapID != ownMapId)
                            break;
                    }
                }
            }
        }
    }

    private void OnArtilleryNewLink(EntityUid uid, BluespaceArtilleryComponent comp, ref NewLinkEvent args)
    {
        if (args.SinkPort != comp.LinkingPort || !HasComp<BluespaceArtilleryConsoleComponent>(args.Source))
            return;

        comp.LinkedConsole = args.Source;
        if (TryComp<BluespaceArtilleryConsoleComponent>(args.Source, out var console))
        {
            console.LinkedArtillery = uid;
            UpdateUI(args.Source, console);
        }
    }

    private void OnConsoleNewLink(EntityUid uid, BluespaceArtilleryConsoleComponent comp, ref NewLinkEvent args)
    {
        if (args.SourcePort != comp.LinkingPort || !HasComp<BluespaceArtilleryComponent>(args.Sink))
            return;

        comp.LinkedArtillery = args.Sink;
        if (TryComp<BluespaceArtilleryComponent>(args.Sink, out var artillery))
        {
            artillery.LinkedConsole = uid;
            UpdateUI(uid, comp);
        }
    }

    private void OnArtilleryLinkAttempt(EntityUid uid, BluespaceArtilleryComponent comp, ref LinkAttemptEvent args)
    {
        if (comp.LinkedConsole != null)
            args.Cancel();
    }

    private void OnConsoleLinkAttempt(EntityUid uid, BluespaceArtilleryConsoleComponent comp, ref LinkAttemptEvent args)
    {
        if (comp.LinkedArtillery != null)
            args.Cancel();
    }

    private void OnArtilleryPortDisconnected(EntityUid uid, BluespaceArtilleryComponent comp, ref PortDisconnectedEvent args)
    {
        if (args.Port != comp.LinkingPort || comp.LinkedConsole == null)
            return;

        if (TryComp<BluespaceArtilleryConsoleComponent>(comp.LinkedConsole, out var console))
            console.LinkedArtillery = null;
        comp.LinkedConsole = null;
    }

    private void OnConsolePortDisconnected(EntityUid uid, BluespaceArtilleryConsoleComponent comp, ref PortDisconnectedEvent args)
    {
        if (args.Port != comp.LinkingPort || comp.LinkedArtillery == null)
            return;

        if (TryComp<BluespaceArtilleryComponent>(comp.LinkedArtillery, out var artillery))
            artillery.LinkedConsole = null;
        comp.LinkedArtillery = null;
    }

    private void OnFireMessage(EntityUid uid, BluespaceArtilleryConsoleComponent console, BluespaceArtilleryFireMessage args)
    {
        if (!HasAccess(uid, args.Actor))
            return;

        if (console.LinkedArtillery == null || !TryComp<BluespaceArtilleryComponent>(console.LinkedArtillery.Value, out var artillery))
            return;

        var artilleryUid = console.LinkedArtillery.Value;
        if (artillery.IsCharging)
            return;

        if (!_powerReceiver.IsPowered(uid) || !IsArtilleryPowered(artilleryUid))
        {
            _popup.PopupEntity(Loc.GetString("bluespace-artillery-no-power"), uid, args.Actor);
            return;
        }

        if (_timing.CurTime < artillery.NextFireTime)
        {
            _popup.PopupEntity(Loc.GetString("bluespace-artillery-on-cooldown"), uid, args.Actor);
            return;
        }

        if (console.TargetObject != null && TryGetTargetObjectWorldPosition(console.TargetObject.Value, out var lockedPos))
        {
            console.TargetMapId = lockedPos.MapId;
            console.TargetCoordinates = new ArtilleryVector2(lockedPos.X, lockedPos.Y);
        }

        var lockedTarget = console.TargetObject;
        var fallbackMapId = console.TargetMapId ?? Transform(uid).MapID;
        var fallbackCoords = console.TargetCoordinates;
        var totalIntensity = Math.Clamp(console.TotalIntensity, MinTotalIntensity, MaxTotalIntensity);
        var slope = Math.Clamp(console.Slope, MinSlope, MaxSlope);
        var maxIntensity = Math.Clamp(console.MaxIntensity, MinMaxIntensity, MaxMaxIntensity);

        artillery.IsCharging = true;
        SetArtilleryPowerDraw(artilleryUid, artillery.ChargePowerDraw);
        UpdateUI(uid, console);

        SetArtilleryVisualState(artilleryUid, BluespaceArtilleryVisualState.Charging);

        _audio.PlayPvs(artillery.SectorChargeSound, artilleryUid,
            AudioParams.Default.WithVolume(1f).WithMaxDistance(10000f));

        var message = Loc.GetString(
            "bluespace-artillery-station-announcement",
            ("coords", $"{fallbackCoords.X:F1}, {fallbackCoords.Y:F1}"));

        var announcementColor = new Color(194, 37, 50); // #c22532
        var announcementSound = _audio.ResolveSound(new SoundPathSpecifier("/Audio/_Sunrise/Announcements/sunrise_artillery.ogg"));
        if (announcementSound != null)
            _audioServer.PlayGlobal(announcementSound, Filter.Broadcast(), false, AudioParams.Default.WithVolume(1f));

        _chat.DispatchGlobalAnnouncement(
            message,
            sender: Loc.GetString("bluespace-artillery-cc-sender"),
            colorOverride: announcementColor);

        _audio.PlayPvs(artillery.ChargeSound, artilleryUid,
            AudioParams.Default.WithVolume(10f).WithMaxDistance(50f));

        Timer.Spawn(TimeSpan.FromSeconds(artillery.ChargeDuration), () =>
        {
            OnChargeCompleted(uid, artilleryUid, lockedTarget, fallbackMapId, fallbackCoords, totalIntensity, slope, maxIntensity);
        });
    }

    private void OnChargeCompleted(
        EntityUid consoleUid,
        EntityUid artilleryUid,
        EntityUid? lockedTarget,
        MapId fallbackMapId,
        ArtilleryVector2 fallbackCoords,
        float totalIntensity,
        float slope,
        float maxIntensity)
    {
        if (Deleted(artilleryUid) || !TryComp<BluespaceArtilleryComponent>(artilleryUid, out var artillery))
            return;

        SetArtilleryPowerDraw(artilleryUid, artillery.IdlePowerDraw);
        SetArtilleryVisualState(artilleryUid, BluespaceArtilleryVisualState.Fire);

        Timer.Spawn(TimeSpan.FromSeconds(3.9), () =>
        {
            if (!Deleted(artilleryUid))
                SetArtilleryVisualState(artilleryUid, BluespaceArtilleryVisualState.Idle);
        });

        _audio.PlayPvs(artillery.FireSound, artilleryUid, AudioParams.Default.WithVolume(5f));

        var impactSound = _audio.ResolveSound(artillery.ImpactSound);
        if (impactSound != null)
            _audioServer.PlayGlobal(impactSound, Filter.Broadcast(), false, AudioParams.Default.WithVolume(1f));

        Timer.Spawn(TimeSpan.FromSeconds(artillery.FlightDuration), () =>
        {
            OnImpact(consoleUid, artilleryUid, lockedTarget, fallbackMapId, fallbackCoords, totalIntensity, slope, maxIntensity);
        });
    }

    private void OnImpact(
        EntityUid consoleUid,
        EntityUid artilleryUid,
        EntityUid? lockedTarget,
        MapId fallbackMapId,
        ArtilleryVector2 fallbackCoords,
        float totalIntensity,
        float slope,
        float maxIntensity)
    {
        MapCoordinates mapCoords;
        if (lockedTarget != null && TryGetTargetObjectWorldPosition(lockedTarget.Value, out var trackedCoords))
        {
            mapCoords = trackedCoords;
        }
        else
        {
            mapCoords = new MapCoordinates(
                new Vector2(fallbackCoords.X, fallbackCoords.Y),
                fallbackMapId);
        }

        var explosionProto = GetExplosionPrototype(DefaultExplosionType);

        _explosion.QueueExplosion(
            mapCoords,
            explosionProto,
            totalIntensity,
            slope,
            maxIntensity,
            null);

        if (!Deleted(artilleryUid) && TryComp<BluespaceArtilleryComponent>(artilleryUid, out var artillery))
        {
            artillery.NextFireTime = _timing.CurTime + TimeSpan.FromSeconds(artillery.CooldownDuration);
            artillery.IsCharging = false;
        }

        if (!Deleted(consoleUid) && TryComp<BluespaceArtilleryConsoleComponent>(consoleUid, out var console))
            UpdateUI(consoleUid, console);
    }

    private string GetExplosionPrototype(string type)
    {
        if (_prototypeManager.TryIndex<ExplosionPrototype>(type, out _))
            return type;

        return ExplosionSystem.DefaultExplosionPrototypeId;
    }

    private void OnSelectTargetStationMessage(EntityUid uid, BluespaceArtilleryConsoleComponent console, BluespaceArtillerySelectTargetStationMessage args)
    {
        if (!HasAccess(uid, args.Actor))
            return;

        if (TryGetEntity(args.Station, out var stationUid) &&
            TryComp<StationDataComponent>(stationUid, out var data))
        {
            console.TargetStation = stationUid;
            console.TargetObject = null;
            var mainGrid = _station.GetLargestGrid((stationUid.Value, data));
            if (mainGrid != null)
            {
                var xform = Transform(mainGrid.Value);
                console.TargetMapId = xform.MapID;
                var pos = _transform.GetWorldPosition(mainGrid.Value);
                console.TargetCoordinates = new ArtilleryVector2(pos.X, pos.Y);
            }
            UpdateUI(uid, console);
        }
    }

    private void OnSelectTargetObjectMessage(EntityUid uid, BluespaceArtilleryConsoleComponent console, BluespaceArtillerySelectTargetObjectMessage args)
    {
        if (!HasAccess(uid, args.Actor))
            return;

        if (args.TargetObject == null)
        {
            console.TargetObject = null;
            UpdateUI(uid, console);
            return;
        }

        if (TryGetEntity(args.TargetObject.Value, out var targetUid) &&
            HasComp<MapGridComponent>(targetUid.Value) &&
            TryGetTargetObjectWorldPosition(targetUid.Value, out var targetCoords))
        {
            console.TargetObject = targetUid.Value;
            console.TargetMapId = targetCoords.MapId;
            console.TargetCoordinates = new ArtilleryVector2(targetCoords.X, targetCoords.Y);
            UpdateUI(uid, console);
        }
    }

    private void OnSetCoordsMessage(EntityUid uid, BluespaceArtilleryConsoleComponent console, BluespaceArtillerySetCoordsMessage args)
    {
        if (!HasAccess(uid, args.Actor))
            return;

        console.TargetObject = null;
        console.TargetCoordinates = args.Coordinates;
        UpdateUI(uid, console);
    }

    private void OnSetParamsMessage(EntityUid uid, BluespaceArtilleryConsoleComponent console, BluespaceArtillerySetParamsMessage args)
    {
        if (!HasAccess(uid, args.Actor))
            return;

        console.ExplosionType = DefaultExplosionType;
        console.TotalIntensity = Math.Clamp(args.TotalIntensity, MinTotalIntensity, MaxTotalIntensity);
        console.Slope = Math.Clamp(args.Slope, MinSlope, MaxSlope);
        console.MaxIntensity = Math.Clamp(args.MaxIntensity, MinMaxIntensity, MaxMaxIntensity);
        UpdateUI(uid, console);
    }

    private void OnPreviewMessage(EntityUid uid, BluespaceArtilleryConsoleComponent console, BluespaceArtilleryPreviewMessage args)
    {
        if (!HasAccess(uid, args.Actor))
            return;

        console.PreviewEnabled = args.Enabled;
        UpdateUI(uid, console);
    }

    private void OnAfterActivatableUIOpen(EntityUid uid, BluespaceArtilleryConsoleComponent console, AfterActivatableUIOpenEvent args)
    {
        if (console.TargetStation == null || console.TargetMapId == null)
            InitTargetStation(uid, console);
        UpdateUI(uid, console);
    }

    private void OnConsoleShutdown(EntityUid uid, BluespaceArtilleryConsoleComponent console, ComponentShutdown args)
    {
        if (console.LinkedArtillery != null && TryComp<BluespaceArtilleryComponent>(console.LinkedArtillery.Value, out var artillery))
            artillery.LinkedConsole = null;
    }

    private void OnArtilleryShutdown(EntityUid uid, BluespaceArtilleryComponent artillery, ComponentShutdown args)
    {
        SetArtilleryPowerDraw(uid, artillery.IdlePowerDraw);

        if (artillery.LinkedConsole != null && TryComp<BluespaceArtilleryConsoleComponent>(artillery.LinkedConsole.Value, out var console))
        {
            console.LinkedArtillery = null;
            UpdateUI(artillery.LinkedConsole.Value, console);
        }
    }

    private void UpdateUI(EntityUid consoleUid, BluespaceArtilleryConsoleComponent console)
    {
        bool isLinked = false;
        bool isArtilleryPowered = false;
        bool isCharging = false;
        bool isOnCooldown = false;
        float cooldownRemaining = 0f;

        if (console.LinkedArtillery != null && TryComp<BluespaceArtilleryComponent>(console.LinkedArtillery.Value, out var artillery))
        {
            isLinked = true;
            isArtilleryPowered = IsArtilleryPowered(console.LinkedArtillery.Value);
            isCharging = artillery.IsCharging;
            isOnCooldown = !artillery.IsCharging && _timing.CurTime < artillery.NextFireTime;

            if (isOnCooldown)
            {
                var diff = artillery.NextFireTime - _timing.CurTime;
                cooldownRemaining = (float)diff.TotalSeconds;
            }
        }

        if (console.TargetObject != null)
        {
            if (TryGetTargetObjectWorldPosition(console.TargetObject.Value, out var trackedCoords))
            {
                console.TargetMapId = trackedCoords.MapId;
                console.TargetCoordinates = new ArtilleryVector2(trackedCoords.X, trackedCoords.Y);
            }
            else
            {
                console.TargetObject = null;
            }
        }

        var availableStations = new Dictionary<NetEntity, string>();
        foreach (var stationUid in _station.GetStations())
        {
            var name = MetaData(stationUid).EntityName;
            if (string.IsNullOrWhiteSpace(name))
                name = Loc.GetString("station-name-default");
            availableStations[GetNetEntity(stationUid)] = name;
        }

        NetEntity? selectedStation = console.TargetStation != null ? GetNetEntity(console.TargetStation.Value) : null;

        var targetMapId = console.TargetMapId ?? Transform(consoleUid).MapID;
        var availableTargets = new Dictionary<NetEntity, string>();
        var gridQuery = EntityQueryEnumerator<MapGridComponent, TransformComponent, PhysicsComponent, FixturesComponent>();
        while (gridQuery.MoveNext(out var gridUid, out _, out var gridXform, out var gridBody, out _))
        {
            if (gridXform.MapID != targetMapId)
                continue;

            _iffQuery.TryComp(gridUid, out var iff);
            if (!_shuttle.CanDraw(gridUid, gridBody, iff))
                continue;

            var label = _shuttle.GetIFFLabel(gridUid, self: false, iff);
            if (string.IsNullOrWhiteSpace(label))
                label = Loc.GetString("shuttle-console-unknown");

            availableTargets[GetNetEntity(gridUid)] = label;
        }

        NetEntity? selectedTarget = console.TargetObject != null && Exists(console.TargetObject.Value)
            ? GetNetEntity(console.TargetObject.Value)
            : null;

        NavInterfaceState? navState = null;
        var mapUid = _mapSystem.GetMapOrInvalid(targetMapId);

        if (mapUid != EntityUid.Invalid)
        {
            var centerCoords = new EntityCoordinates(mapUid, new Vector2(console.TargetCoordinates.X, console.TargetCoordinates.Y));
            var docks = _shuttleConsole.GetAllDocks();
            navState = new NavInterfaceState(512f, GetNetCoordinates(centerCoords), Angle.Zero, docks);
            navState.RotateWithEntity = false;
        }

        var state = new BluespaceArtilleryConsoleBoundUserInterfaceState(
            console.TargetCoordinates,
            DefaultExplosionType,
            Math.Clamp(console.TotalIntensity, MinTotalIntensity, MaxTotalIntensity),
            Math.Clamp(console.Slope, MinSlope, MaxSlope),
            Math.Clamp(console.MaxIntensity, MinMaxIntensity, MaxMaxIntensity),
            console.PreviewEnabled,
            isLinked,
            isArtilleryPowered,
            isCharging,
            isOnCooldown,
            cooldownRemaining,
            availableStations,
            selectedStation,
            availableTargets,
            selectedTarget,
            navState
        );

        _ui.SetUiState(consoleUid, BluespaceArtilleryConsoleUiKey.Key, state);
    }
}
