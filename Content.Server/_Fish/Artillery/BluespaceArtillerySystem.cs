using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.DeviceNetwork;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Fish.Artillery;
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
using Robust.Server.GameObjects;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
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
    [Dependency] private IMapManager _mapManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BluespaceArtilleryComponent, NewLinkEvent>(OnArtilleryNewLink);
        SubscribeLocalEvent<BluespaceArtilleryComponent, PortDisconnectedEvent>(OnArtilleryPortDisconnected);
        SubscribeLocalEvent<BluespaceArtilleryComponent, LinkAttemptEvent>(OnArtilleryLinkAttempt);
		SubscribeLocalEvent<BluespaceArtilleryComponent, MapInitEvent>(OnArtilleryMapInit);
        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, LinkAttemptEvent>(OnConsoleLinkAttempt);
        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, PortDisconnectedEvent>(OnConsolePortDisconnected);
		SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, MapInitEvent>(OnConsoleMapInit);
        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, NewLinkEvent>(OnConsoleNewLink);

        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, BluespaceArtilleryFireMessage>(OnFireMessage);
        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, BluespaceArtillerySetCoordsMessage>(OnSetCoordsMessage);
        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, BluespaceArtillerySetParamsMessage>(OnSetParamsMessage);
        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, BluespaceArtilleryPreviewMessage>(OnPreviewMessage);
        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, BluespaceArtillerySelectTargetStationMessage>(OnSelectTargetStationMessage);
        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, AfterActivatableUIOpenEvent>(OnAfterActivatableUIOpen);

        SubscribeLocalEvent<BluespaceArtilleryConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
        SubscribeLocalEvent<BluespaceArtilleryComponent, ComponentShutdown>(OnArtilleryShutdown);
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
        if (console.LinkedArtillery == null)
            return;

        var artillery = Comp<BluespaceArtilleryComponent>(console.LinkedArtillery.Value);
        if (artillery.IsCharging)
            return;

		if (!_powerReceiver.IsPowered(uid) ||
			!_powerReceiver.IsPowered(console.LinkedArtillery.Value))
		{
			_popup.PopupEntity(Loc.GetString("bluespace-artillery-no-power"), uid, uid);
			return;
		}
		
        if (_timing.CurTime < artillery.NextFireTime)
        {
            _popup.PopupEntity(Loc.GetString("bluespace-artillery-on-cooldown"), uid, uid);
            return;
        }

        artillery.IsCharging = true;
        UpdateUI(uid, console);
		
		SetArtilleryVisualState(console.LinkedArtillery.Value, BluespaceArtilleryVisualState.Charging);

		EntityUid? targetStation = null;
		if (console.TargetMapId != null)
		{
			foreach (var station in EntityQuery<StationMemberComponent>())
			{
				if (Transform(station.Owner).MapID == console.TargetMapId)
				{
					targetStation = station.Owner;
					break;
				}
			}
		}

		_audio.PlayPvs(artillery.SectorChargeSound, console.LinkedArtillery.Value, 
			AudioParams.Default.WithVolume(1f).WithMaxDistance(10000f));

		var message = Loc.GetString(
			"bluespace-artillery-station-announcement",
			("coords", $"{console.TargetCoordinates.X:F1}, {console.TargetCoordinates.Y:F1}"));

		var announcementColor = new Color(194, 37, 50); // #c22532
		var announcementSound = _audio.ResolveSound(new SoundPathSpecifier("/Audio/_Sunrise/Announcements/sunrise_artillery.ogg"));
		if (announcementSound != null)
			_audioServer.PlayGlobal(announcementSound, Filter.Broadcast(), false, AudioParams.Default.WithVolume(1f));
		
		
		_chat.DispatchGlobalAnnouncement(
			message,
			sender: Loc.GetString("bluespace-artillery-cc-sender"),
			colorOverride: announcementColor);

        _audio.PlayPvs(artillery.ChargeSound, console.LinkedArtillery.Value,
            AudioParams.Default.WithVolume(10f).WithMaxDistance(50f));

        Timer.Spawn(TimeSpan.FromSeconds(artillery.ChargeDuration), () =>
        {
            OnChargeCompleted(uid, console, artillery);
        });
    }

	private void OnChargeCompleted(EntityUid consoleUid, BluespaceArtilleryConsoleComponent console, BluespaceArtilleryComponent artillery)
	{
		if (console.LinkedArtillery == null)
			return;

		SetArtilleryVisualState(console.LinkedArtillery.Value, BluespaceArtilleryVisualState.Fire);
		Timer.Spawn(TimeSpan.FromSeconds(3.9), () =>
		{
			if (!Deleted(console.LinkedArtillery.Value))
				SetArtilleryVisualState(console.LinkedArtillery.Value, BluespaceArtilleryVisualState.Idle);
		});

		_audio.PlayPvs(artillery.FireSound, console.LinkedArtillery.Value, AudioParams.Default.WithVolume(5f));
		
		var impactSound = _audio.ResolveSound(artillery.ImpactSound);
		if (impactSound != null)
			_audioServer.PlayGlobal(impactSound, Filter.Broadcast(), false, AudioParams.Default.WithVolume(1f));

		Timer.Spawn(TimeSpan.FromSeconds(artillery.FlightDuration), () =>
		{
			OnImpact(consoleUid, console, artillery);
		});
	}

    private void OnImpact(EntityUid consoleUid, BluespaceArtilleryConsoleComponent console, BluespaceArtilleryComponent artillery)
    {
        if (console.LinkedArtillery == null)
            return;

        var mapId = console.TargetMapId ?? Transform(consoleUid).MapID;
		var mapCoords = new MapCoordinates(
			new Vector2(console.TargetCoordinates.X, console.TargetCoordinates.Y),
			mapId);

        var explosionProto = GetExplosionPrototype(console.ExplosionType);

        _explosion.QueueExplosion(
            mapCoords,
            explosionProto,
            console.TotalIntensity,
            console.Slope,
            console.MaxIntensity,
            null);

        artillery.NextFireTime = _timing.CurTime + TimeSpan.FromSeconds(artillery.CooldownDuration);
        artillery.IsCharging = false;
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
        if (TryGetEntity(args.Station, out var stationUid) &&
            TryComp<StationDataComponent>(stationUid, out var data))
        {
            console.TargetStation = stationUid;
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

    private void OnSetCoordsMessage(EntityUid uid, BluespaceArtilleryConsoleComponent console, BluespaceArtillerySetCoordsMessage args)
    {
        console.TargetCoordinates = args.Coordinates;
        UpdateUI(uid, console);
    }

    private void OnSetParamsMessage(EntityUid uid, BluespaceArtilleryConsoleComponent console, BluespaceArtillerySetParamsMessage args)
    {
        console.ExplosionType = args.ExplosionType;
        console.TotalIntensity = args.TotalIntensity;
        console.Slope = args.Slope;
        console.MaxIntensity = args.MaxIntensity;
        UpdateUI(uid, console);
    }

    private void OnPreviewMessage(EntityUid uid, BluespaceArtilleryConsoleComponent console, BluespaceArtilleryPreviewMessage args)
    {
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
        if (artillery.LinkedConsole != null && TryComp<BluespaceArtilleryConsoleComponent>(artillery.LinkedConsole.Value, out var console))
        {
            console.LinkedArtillery = null;
            UpdateUI(artillery.LinkedConsole.Value, console);
        }
    }

	private void UpdateUI(EntityUid consoleUid, BluespaceArtilleryConsoleComponent console)
	{
		bool isLinked = false;
		bool isCharging = false;
		bool isOnCooldown = false;
		float cooldownRemaining = 0f;

		if (console.LinkedArtillery != null && TryComp<BluespaceArtilleryComponent>(console.LinkedArtillery.Value, out var artillery))
		{
			isLinked = true;
			isCharging = artillery.IsCharging;
			isOnCooldown = !artillery.IsCharging && _timing.CurTime < artillery.NextFireTime;

			if (isOnCooldown)
			{
				var diff = artillery.NextFireTime - _timing.CurTime;
				cooldownRemaining = (float)diff.TotalSeconds;
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

		NavInterfaceState? navState = null;
		var targetMapId = console.TargetMapId ?? Transform(consoleUid).MapID;
		var mapUid = _mapManager.GetMapEntityId(targetMapId);

		if (mapUid != EntityUid.Invalid)
		{
			var centerCoords = new EntityCoordinates(mapUid, new Vector2(console.TargetCoordinates.X, console.TargetCoordinates.Y));
			var docks = _shuttleConsole.GetAllDocks();
			navState = new NavInterfaceState(512f, GetNetCoordinates(centerCoords), Angle.Zero, docks);
			navState.RotateWithEntity = false;
		}

		var state = new BluespaceArtilleryConsoleBoundUserInterfaceState(
			console.TargetCoordinates,
			console.ExplosionType,
			console.TotalIntensity,
			console.Slope,
			console.MaxIntensity,
			console.PreviewEnabled,
			isLinked,
			isCharging,
			isOnCooldown,
			cooldownRemaining,
			availableStations,
			selectedStation,
			navState
		);

		_ui.SetUiState(consoleUid, BluespaceArtilleryConsoleUiKey.Key, state);
	}
}