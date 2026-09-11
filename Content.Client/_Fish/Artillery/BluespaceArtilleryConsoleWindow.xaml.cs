using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.Shuttles.UI;
using Content.Shared._Fish.Artillery;
using Content.Shared.Explosion;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Collections;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Fish.Artillery;

public sealed partial class ArtilleryScannerControl : BaseShuttleControl
{
    [Dependency] private IMapManager _mapManager = default!;
    private SharedShuttleSystem _shuttles;
    private SharedTransformSystem _transform;

    private EntityCoordinates? _coordinates;
    private Angle? _rotation;
    private Dictionary<NetEntity, List<DockingPortState>> _docks = new();
    private List<Entity<MapGridComponent>> _grids = new();

    private Vector2 ScannerCenter => PixelSize / 2f;
    private float ScannerRadius => MathF.Min(PixelWidth, PixelHeight) / 2f;
    private float ScannerScale => WorldRange != 0 ? ScannerRadius / WorldRange : 0f;

    public Action<EntityCoordinates>? OnRadarClick;

    public bool PreviewEnabled { get; set; }
    public float PreviewRadius { get; set; }

    public ArtilleryScannerControl() : base(64f, 1024f, 256f)
    {
        _shuttles = EntManager.System<SharedShuttleSystem>();
        _transform = EntManager.System<SharedTransformSystem>();
    }

    public void UpdateNavState(NavInterfaceState? state, ArtilleryVector2 targetCoords)
    {
        if (state == null || state.Coordinates == null)
        {
            _coordinates = null;
            _rotation = null;
            _docks.Clear();
            return;
        }

        _coordinates = EntManager.GetCoordinates(state.Coordinates);
        _rotation = state.Angle ?? Angle.Zero;
        _docks = state.Docks;

        WorldMaxRange = state.MaxRange;
        if (WorldMaxRange < WorldRange)
            ActualRadarRange = WorldMaxRange;
        if (WorldMaxRange < WorldMinRange)
            WorldMinRange = WorldMaxRange;

        ActualRadarRange = Math.Clamp(ActualRadarRange, WorldMinRange, WorldMaxRange);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (_coordinates == null || _rotation == null || args.Function != EngineKeyFunctions.UIClick ||
            OnRadarClick == null)
        {
            return;
        }

        var a = InverseScalePosition(args.RelativePosition);
        var relativeWorldPos = new Vector2(a.X, -a.Y);
        relativeWorldPos = _rotation.Value.RotateVec(relativeWorldPos);
        var coords = _coordinates.Value.Offset(relativeWorldPos);
        OnRadarClick?.Invoke(coords);
    }

    private Vector2 InverseScalePosition(Vector2 value)
    {
        return (value - ScannerCenter) / ScannerScale;
    }

    private new void DrawCircles(DrawingHandleScreen handle)
    {
        var gridLines = Color.LightGray.WithAlpha(0.01f);
        const float EquatorialMultiplier = 2f;

        var minDistance = MathF.Pow(EquatorialMultiplier, EquatorialMultiplier * 1.5f);
        var maxDistance = MathF.Pow(2f, EquatorialMultiplier * 6f);
        var cornerDistance = MathF.Sqrt(WorldRange * WorldRange + WorldRange * WorldRange);

        var origin = ScannerCenter;

        for (var radius = minDistance; radius <= maxDistance; radius *= EquatorialMultiplier)
        {
            if (radius > cornerDistance)
                continue;

            var color = Color.ToSrgb(gridLines).WithAlpha(0.05f);
            var scaledRadius = ScannerScale * radius;
            var text = $"{radius:0}m";
            var textDimensions = handle.GetDimensions(Font, text, UIScale);

            handle.DrawCircle(origin, scaledRadius, color, false);
            handle.DrawString(Font, origin + new Vector2(0f, -scaledRadius) - new Vector2(0f, textDimensions.Y), text, UIScale, color);
        }

        const int gridLinesRadial = 8;
        for (var i = 0; i < gridLinesRadial; i++)
        {
            Angle angle = (Math.PI / gridLinesRadial) * i;
            var aExtent = angle.ToVec() * ScannerRadius * 1.42f;
            var lineColor = Color.MediumSpringGreen.WithAlpha(0.02f);
            handle.DrawLine(origin - aExtent, origin + aExtent, lineColor);
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        DrawBacking(handle);
        DrawCircles(handle);

        if (_coordinates == null || _rotation == null)
        {
            DrawNoSignal(handle);
            return;
        }

        var xformQuery = EntManager.GetEntityQuery<TransformComponent>();
        var fixturesQuery = EntManager.GetEntityQuery<FixturesComponent>();
        var bodyQuery = EntManager.GetEntityQuery<PhysicsComponent>();

        if (!xformQuery.TryGetComponent(_coordinates.Value.EntityId, out var xform)
            || xform.MapID == MapId.Nullspace)
        {
            DrawNoSignal(handle);
            return;
        }

        var mapPos = _transform.ToMapCoordinates(_coordinates.Value);
        var posMatrix = Matrix3Helpers.CreateTransform(_coordinates.Value.Position, _rotation.Value);
        var ourEntRot = _rotation.Value;
        var ourEntMatrix = Matrix3Helpers.CreateTransform(_transform.GetWorldPosition(xform), ourEntRot);
        var centerToWorld = Matrix3x2.Multiply(posMatrix, ourEntMatrix);
        Matrix3x2.Invert(centerToWorld, out var worldToCenter);
        var centerToView = Matrix3x2.CreateScale(new Vector2(ScannerScale, -ScannerScale)) * Matrix3x2.CreateTranslation(ScannerCenter);

        var rot = ourEntRot + _rotation.Value;
        var viewBounds = new Box2Rotated(new Box2(-WorldRange, -WorldRange, WorldRange, WorldRange).Translated(mapPos.Position), rot, mapPos.Position);
        var viewAABB = viewBounds.CalcBoundingBox();

        _grids.Clear();
        _mapManager.FindGridsIntersecting(xform.MapID, new Box2(mapPos.Position - MaxRadarRangeVector, mapPos.Position + MaxRadarRangeVector), ref _grids, approx: true, includeMap: false);

        foreach (var grid in _grids)
        {
            var gUid = grid.Owner;
            if (!fixturesQuery.HasComponent(gUid))
                continue;

            var gridBody = bodyQuery.GetComponent(gUid);
            EntManager.TryGetComponent<IFFComponent>(gUid, out var iff);

            if (!_shuttles.CanDraw(gUid, gridBody, iff))
                continue;

            var curGridToWorld = _transform.GetWorldMatrix(gUid);
            var curGridToView = curGridToWorld * worldToCenter * centerToView;
            var labelColor = _shuttles.GetIFFColor(grid, self: false, iff);
            var coordColor = new Color(labelColor.R * 0.8f, labelColor.G * 0.8f, labelColor.B * 0.8f, 0.5f);
            var labelName = _shuttles.GetIFFLabel(grid, self: false, iff);

            if (labelName != null)
            {
                var gridBounds = grid.Comp.LocalAABB;
                var gridCentre = Vector2.Transform(gridBody.LocalCenter, curGridToView);
                var gridDistance = (gridBody.LocalCenter - xform.LocalPosition).Length();
                var labelText = Loc.GetString("shuttle-console-iff-label", ("name", labelName), ("distance", $"{gridDistance:0.0}"));
                var mapCoords = _transform.GetWorldPosition(gUid);
                var coordsText = $"({mapCoords.X:0.0}, {mapCoords.Y:0.0})";

                var labelDimensions = handle.GetDimensions(Font, labelText, 1f);
                var coordsDimensions = handle.GetDimensions(Font, coordsText, 0.7f);
                var yOffset = Math.Max(gridBounds.Height, gridBounds.Width) * ScannerScale / 1.8f;
                var gridScaledPosition = gridCentre - new Vector2(0, -yOffset);

                var gridOffset = gridScaledPosition / PixelSize - new Vector2(0.5f, 0.5f);
                var offsetMax = Math.Max(Math.Abs(gridOffset.X), Math.Abs(gridOffset.Y)) * 2f;
                if (offsetMax > 1)
                {
                    gridOffset = new Vector2(gridOffset.X / offsetMax, gridOffset.Y / offsetMax);
                    gridScaledPosition = (gridOffset + new Vector2(0.5f, 0.5f)) * PixelSize;
                }

                var labelUiPosition = gridScaledPosition - new Vector2(labelDimensions.X / 2f, 0);
                var coordUiPosition = gridScaledPosition - new Vector2(coordsDimensions.X / 2f, -labelDimensions.Y);
                var controlExtents = PixelSize - new Vector2(labelDimensions.X, labelDimensions.Y);
                labelUiPosition = Vector2.Clamp(labelUiPosition, Vector2.Zero, controlExtents);

                handle.DrawString(Font, labelUiPosition, labelText, labelColor);

                if (offsetMax < 1)
                {
                    handle.DrawString(Font, coordUiPosition, coordsText, 0.7f, coordColor);
                }
            }

            var gridAABB = curGridToWorld.TransformBox(grid.Comp.LocalAABB);
            if (!gridAABB.Intersects(viewAABB))
                continue;

            DrawGrid(handle, curGridToView, grid, labelColor);
            DrawDocks(handle, gUid, curGridToView);
        }

        if (PreviewEnabled && PreviewRadius > 0f)
        {
            var screenRadius = PreviewRadius * ScannerScale;
            handle.DrawCircle(ScannerCenter, screenRadius, new Color(255, 60, 60, 40), true);
            handle.DrawCircle(ScannerCenter, screenRadius, new Color(255, 80, 80, 180), false);
        }
    }

    private void DrawDocks(DrawingHandleScreen handle, EntityUid uid, Matrix3x2 gridToView)
    {
        const float DockScale = 0.6f;
        var nent = EntManager.GetNetEntity(uid);

        const float sqrt2 = 1.41421356f;
        const float dockRadius = DockScale * sqrt2;
        Box2 viewBounds = new Box2(
            -dockRadius * UIScale,
            -dockRadius * UIScale,
            (Size.X + dockRadius) * UIScale,
            (Size.Y + dockRadius) * UIScale);

        if (_docks.TryGetValue(nent, out var docks))
        {
            foreach (var state in docks)
            {
                var position = state.Coordinates.Position;
                var positionInView = Vector2.Transform(position, gridToView);
                if (!viewBounds.Contains(positionInView))
                    continue;

                var color = Color.ToSrgb(state.HighlightedColor);
                var verts = new[]
                {
                    Vector2.Transform(position + new Vector2(-DockScale, -DockScale), gridToView),
                    Vector2.Transform(position + new Vector2(DockScale, -DockScale), gridToView),
                    Vector2.Transform(position + new Vector2(DockScale, DockScale), gridToView),
                    Vector2.Transform(position + new Vector2(-DockScale, DockScale), gridToView),
                };

                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, verts, color.WithAlpha(0.8f));
                handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, verts, color);
            }
        }
    }
}

public sealed partial class BluespaceArtilleryConsoleWindow : DefaultWindow
{
    private readonly OptionButton _stationSelector;
    private readonly LineEdit _coordinateX;
    private readonly LineEdit _coordinateY;
    private readonly Button _applyCoordinates;
    private readonly Label _currentCoords;
    private readonly Label _statusLabel;
    private readonly BoxContainer _scannerContainer;
    private readonly ArtilleryScannerControl _scannerControl;
    private readonly OptionButton _explosionType;
    private readonly LineEdit _intensity;
    private readonly LineEdit _slope;
    private readonly LineEdit _maxIntensity;
    private readonly CheckBox _previewToggle;
    private readonly Button _fireButton;

    private bool _isLinked;
    private bool _isCharging;
    private bool _isOnCooldown;
    private float _cooldownRemaining;
    private bool _cooldownTickActive;

    private ArtilleryVector2 _targetCoords;
    private const float ScannerMaxOffset = 16384f;

    private readonly List<string> _explosionTypes = new();
    private readonly List<NetEntity> _stationEntities = new();

    public event Action? OnFire;
    public event Action<NetEntity>? OnStationSelected;
    public event Action<ArtilleryVector2>? OnCoordsChanged;
    public event Action<string, float, float, float>? OnParamsChanged;
    public event Action<bool>? OnPreviewToggled;

    public BluespaceArtilleryConsoleWindow()
    {
        RobustXamlLoader.Load(this);

        _stationSelector = this.FindControl<OptionButton>("StationSelector")!;
        _coordinateX = this.FindControl<LineEdit>("CoordinateX")!;
        _coordinateY = this.FindControl<LineEdit>("CoordinateY")!;
        _applyCoordinates = this.FindControl<Button>("ApplyCoordinates")!;
        _currentCoords = this.FindControl<Label>("CurrentCoords")!;
        _statusLabel = this.FindControl<Label>("StatusLabel")!;
        _scannerContainer = this.FindControl<BoxContainer>("ScannerContainer")!;
        _scannerControl = this.FindControl<ArtilleryScannerControl>("ScannerControl")!;
        _explosionType = this.FindControl<OptionButton>("ExplosionType")!;
        _intensity = this.FindControl<LineEdit>("Intensity")!;
        _slope = this.FindControl<LineEdit>("Slope")!;
        _maxIntensity = this.FindControl<LineEdit>("MaxIntensity")!;
        _previewToggle = this.FindControl<CheckBox>("PreviewToggle")!;
        _fireButton = this.FindControl<Button>("FireButton")!;

        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        foreach (var proto in prototypeManager.EnumeratePrototypes<ExplosionPrototype>())
        {
            _explosionTypes.Add(proto.ID);
        }
        _explosionTypes.Sort();

        foreach (var type in _explosionTypes)
            _explosionType.AddItem(type);

        _stationSelector.OnItemSelected += args =>
        {
            if (args.Id >= 0 && args.Id < _stationEntities.Count)
                OnStationSelected?.Invoke(_stationEntities[args.Id]);
        };

        _applyCoordinates.OnPressed += _ => ApplyManualCoordinates();

        _fireButton.OnPressed += _ =>
        {
            SendParams();
            OnFire?.Invoke();
        };

        _previewToggle.OnToggled += args =>
        {
            _scannerControl.PreviewEnabled = args.Pressed;
            UpdatePreviewRadius();
            OnPreviewToggled?.Invoke(args.Pressed);
        };

        _explosionType.OnItemSelected += args =>
        {
            _explosionType.SelectId(args.Id);
            UpdatePreviewRadius();
            SendParams();
        };

        _intensity.OnTextChanged += _ =>
        {
            UpdatePreviewRadius();
            SendParams();
        };
        _slope.OnTextChanged += _ =>
        {
            UpdatePreviewRadius();
            SendParams();
        };
        _maxIntensity.OnTextChanged += _ =>
        {
            UpdatePreviewRadius();
            SendParams();
        };

        _scannerControl.OnRadarClick += coords =>
        {
            _targetCoords = new ArtilleryVector2(coords.Position.X, coords.Position.Y);
            ClampCoordinates();
            UpdateCoordFields();
            OnCoordsChanged?.Invoke(_targetCoords);
        };

        _targetCoords = ArtilleryVector2.Zero;
        UpdateCoordFields();
    }

    private void UpdatePreviewRadius()
    {
        if (float.TryParse(_intensity.Text, out var intensity) &&
            float.TryParse(_slope.Text, out var slope) &&
            slope > 0)
        {
            _scannerControl.PreviewRadius = MathF.Sqrt(intensity / slope);
        }
    }

    private void ApplyManualCoordinates()
    {
        if (float.TryParse(_coordinateX.Text, out var x) &&
            float.TryParse(_coordinateY.Text, out var y))
        {
            _targetCoords = new ArtilleryVector2(x, y);
            ClampCoordinates();
            UpdateCoordFields();
            OnCoordsChanged?.Invoke(_targetCoords);
        }
    }

    private void ClampCoordinates()
    {
        _targetCoords.X = Math.Clamp(_targetCoords.X, -ScannerMaxOffset, ScannerMaxOffset);
        _targetCoords.Y = Math.Clamp(_targetCoords.Y, -ScannerMaxOffset, ScannerMaxOffset);
    }

    private void UpdateCoordFields()
    {
        _coordinateX.Text = _targetCoords.X.ToString("F1");
        _coordinateY.Text = _targetCoords.Y.ToString("F1");
        _currentCoords.Text = $"X: {_targetCoords.X:F1}, Y: {_targetCoords.Y:F1}";
    }

    private void SendParams()
    {
        if (_explosionType.SelectedId < 0 || _explosionType.SelectedId >= _explosionTypes.Count)
            return;

        if (float.TryParse(_intensity.Text, out var intensity) &&
            float.TryParse(_slope.Text, out var slope) &&
            float.TryParse(_maxIntensity.Text, out var max))
        {
            var type = _explosionTypes[_explosionType.SelectedId];
            OnParamsChanged?.Invoke(type, intensity, slope, max);
        }
    }

    private void StartCooldownTick()
    {
        if (_cooldownTickActive)
            return;

        _cooldownTickActive = true;
        TickCooldown();
    }

    private void StopCooldownTick()
    {
        _cooldownTickActive = false;
    }

    private void TickCooldown()
    {
        if (!_cooldownTickActive)
            return;

        _cooldownRemaining -= 0.1f;
        if (_cooldownRemaining <= 0f)
        {
            _cooldownRemaining = 0f;
            _isOnCooldown = false;
            _cooldownTickActive = false;

            UpdateStatusLabel();
            _fireButton.Disabled = !_isLinked || _isCharging;

            return;
        }

        UpdateStatusLabel();
        Timer.Spawn(TimeSpan.FromSeconds(0.1), TickCooldown);
    }

    private void UpdateStatusLabel()
    {
        if (!_isLinked)
            _statusLabel.Text = Loc.GetString("bluespace-artillery-status-no-link");
        else if (_isCharging)
            _statusLabel.Text = Loc.GetString("bluespace-artillery-status-charging");
        else if (_isOnCooldown)
            _statusLabel.Text = $"{Loc.GetString("bluespace-artillery-status-cooldown")} {_cooldownRemaining:F1} с";
        else
            _statusLabel.Text = Loc.GetString("bluespace-artillery-status-ready");
    }

    public void UpdateState(BluespaceArtilleryConsoleBoundUserInterfaceState state)
    {
        _targetCoords = state.TargetCoordinates;
        UpdateCoordFields();

        _stationSelector.Clear();
        _stationEntities.Clear();
        int selectedIndex = 0;
        int index = 0;
        foreach (var (stationEnt, name) in state.AvailableStations)
        {
            _stationEntities.Add(stationEnt);
            _stationSelector.AddItem(name);
            if (state.SelectedStation != null && stationEnt == state.SelectedStation.Value)
                selectedIndex = index;
            index++;
        }

        if (_stationEntities.Count > 0)
            _stationSelector.SelectId(selectedIndex);

        _scannerControl.PreviewEnabled = state.PreviewEnabled;
        _scannerControl.UpdateNavState(state.NavState, _targetCoords);

        var typeIndex = _explosionTypes.IndexOf(state.ExplosionType);
        if (_explosionTypes.Count > 0)
            _explosionType.SelectId(typeIndex >= 0 ? typeIndex : 0);

        _intensity.Text = state.TotalIntensity.ToString();
        _slope.Text = state.Slope.ToString();
        _maxIntensity.Text = state.MaxIntensity.ToString();
        _previewToggle.Pressed = state.PreviewEnabled;
        UpdatePreviewRadius();

        _isLinked = state.IsLinked;
        _isCharging = state.IsCharging;
        _isOnCooldown = state.IsOnCooldown;
        _cooldownRemaining = state.CooldownRemaining;
		
		bool lockAll = state.IsCharging;

		_coordinateX.Editable = !lockAll;
		_coordinateY.Editable = !lockAll;
		_applyCoordinates.Disabled = lockAll;
		_explosionType.Disabled = lockAll;
		_intensity.Editable = !lockAll;
		_slope.Editable = !lockAll;
		_maxIntensity.Editable = !lockAll;
		_previewToggle.Disabled = lockAll;
		_stationSelector.Disabled = lockAll;
		_scannerControl.MouseFilter = lockAll ? MouseFilterMode.Ignore : MouseFilterMode.Stop;

        UpdateStatusLabel();

        if (state.IsOnCooldown)
            StartCooldownTick();
        else
            StopCooldownTick();

        _fireButton.Disabled = !state.IsLinked || state.IsCharging || state.IsOnCooldown;
    }
}