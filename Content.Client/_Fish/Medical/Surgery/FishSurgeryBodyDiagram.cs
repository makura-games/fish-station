using System.Numerics;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Utility;
using Robust.Shared.Localization;
using Robust.Shared.Timing;

namespace Content.Client._Fish.Medical.Surgery;

/// <summary>
/// Front-facing anatomical selector. Only parts supplied by the surgery UI state can be selected.
/// </summary>
public sealed class FishSurgeryBodyDiagram : Control
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;
    [Dependency] private readonly MarkingManager _marking = default!;

    // Координаты общего холста частей тела 32x32. Кисти и стопы имеют расширенные области нажатия.
    private static readonly Region[] Regions =
    [
        new("Head", new(10, 2, 21, 10)),
        new("Torso", new(11, 10, 20, 23)),
        new("ArmLeft", new(20, 10, 26, 18)),
        new("ArmRight", new(5, 10, 11, 18)),
        new("HandLeft", new(20, 18, 26, 23)),
        new("HandRight", new(5, 18, 11, 23)),
        new("LegLeft", new(16, 23, 21, 29)),
        new("LegRight", new(10, 23, 15, 29)),
        new("FootLeft", new(16, 29, 23, 32)),
        new("FootRight", new(8, 29, 15, 32)),
    ];

    private readonly Dictionary<string, Part> _parts = new();
    private static readonly HumanoidVisualLayers[] BodyLayers = Enum.GetValues<HumanoidVisualLayers>();
    private readonly SortedSet<int> _layerIndices = new();
    private readonly List<BodyLayer> _appearance = new();
    private readonly float[] _highlightAmounts = new float[Regions.Length];
    private string? _hoveredCategory;
    private EntityUid? _patient;

    internal IReadOnlyList<BodyLayer> AppearanceLayers => _appearance;

    /// <summary>The actual patient whose anatomy, eyes and markings are displayed without equipment.</summary>
    public EntityUid? Patient
    {
        get => _patient;
        set
        {
            if (_patient == value)
                return;

            _patient = value;
            RefreshAppearance();
        }
    }

    /// <summary>Raised when an available body region is clicked.</summary>
    public event Action<EntityUid>? PartSelected;

    /// <summary>Supplies an immediate, readable label for the hovered region.</summary>
    public event Action<string?>? HoveredPartChanged;

    /// <summary>The part whose operations are currently displayed.</summary>
    public EntityUid? SelectedPart { get; set; }

    public FishSurgeryBodyDiagram()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
    }

    /// <summary>Discards the previous snapshot, including hover and selection.</summary>
    public void ClearParts()
    {
        _parts.Clear();
        Array.Clear(_highlightAmounts);
        _hoveredCategory = null;
        SelectedPart = null;
        ToolTip = null;
        HoveredPartChanged?.Invoke(null);
    }

    /// <summary>Starts a synchronous anatomy refresh while retaining local interaction state.</summary>
    public void BeginPartsUpdate()
    {
        _parts.Clear();
    }

    /// <summary>Removes interaction state only for regions that disappeared from the snapshot.</summary>
    public void EndPartsUpdate()
    {
        var selectedExists = false;
        for (var i = 0; i < Regions.Length; i++)
        {
            if (_parts.TryGetValue(Regions[i].Category, out var part))
                selectedExists |= part.Uid == SelectedPart;
            else
                _highlightAmounts[i] = 0f;
        }
        if (!selectedExists)
            SelectedPart = null;
        if (_hoveredCategory != null && !_parts.ContainsKey(_hoveredCategory))
            _hoveredCategory = null;
        ToolTip = _hoveredCategory != null ? _parts[_hoveredCategory].Name : null;
        HoveredPartChanged?.Invoke(ToolTip);
    }

    /// <summary>
    /// Registers a selectable region. Returns false for additional anatomy that needs a separate selector.
    /// </summary>
    public bool AddPart(Entity<OrganComponent> organ, string name)
    {
        if (organ.Comp.Category?.Id is not { } category || !HasRegion(category))
            return false;

        Texture? texture = null;
        var color = Color.White;
        if (_entities.TryGetComponent<VisualOrganComponent>(organ, out var visual))
        {
            var layer = visual.Data;
            RSI? rsi = null;
            if (layer.RsiPath is { } path &&
                _resources.TryGetResource<RSIResource>(new ResPath("/Textures") / path, out var resource))
                rsi = resource.RSI;
            else if (_entities.TryGetComponent<SpriteComponent>(organ, out var sprite))
                rsi = sprite.BaseRSI;
            if (layer.State is { } stateId && rsi != null && rsi.TryGetState(stateId, out var state))
            {
                texture = state.Frame0;
                color = layer.Color ?? Color.White;
            }
        }

        return _parts.TryAdd(category, new Part(organ, name, texture, color));
    }

    /// <summary>Caches resolved patient layers in sprite order, including inherited RSI and facial details.</summary>
    public void RefreshAppearance()
    {
        _appearance.Clear();
        _layerIndices.Clear();
        if (Patient is not { } patient || !_entities.TryGetComponent<SpriteComponent>(patient, out var sprite))
            return;

        var sprites = _entities.System<SpriteSystem>();
        foreach (var key in BodyLayers)
        {
            if (key is HumanoidVisualLayers.Handcuffs or HumanoidVisualLayers.Ensnare or
                HumanoidVisualLayers.Fire or HumanoidVisualLayers.StencilMask or HumanoidVisualLayers.Overlay)
                continue;
            if (sprites.LayerMapTryGet((patient, sprite), key, out var index, false))
                _layerIndices.Add(index);
        }

        if (_entities.TryGetComponent<BodyComponent>(patient, out var body) && body.Organs != null)
        {
            foreach (var organ in body.Organs.ContainedEntities)
            {
                if (!_entities.TryGetComponent<VisualOrganMarkingsComponent>(organ, out var markings))
                    continue;
                foreach (var marking in markings.AppliedMarkings)
                {
                    if (!_marking.TryGetMarking(marking, out var prototype))
                        continue;
                    foreach (var specifier in prototype.Sprites)
                    {
                        if (specifier is SpriteSpecifier.Rsi rsi &&
                            sprites.LayerMapTryGet((patient, sprite), $"{prototype.ID}-{rsi.RsiState}", out var index, false))
                            _layerIndices.Add(index);
                    }
                }
            }
        }

        foreach (var index in _layerIndices)
        {
            if (!sprites.TryGetLayer((patient, sprite), index, out var layer, false))
                continue;
            var texture = layer.ActualState?.Frame0 ?? layer.Texture;
            if (texture != null)
                _appearance.Add(new BodyLayer(texture, layer.Color, layer.Scale, layer.Offset));
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var canvas = GetCanvas(PixelSize);
        var scale = canvas.Width / 32f;
        var palette = FishSurgerySheetlet.Palette;

        if (Patient != null)
        {
            foreach (var layer in _appearance)
            {
                var size = (Vector2)layer.Texture.Size * layer.Scale * scale;
                var offset = new Vector2(layer.Offset.X, -layer.Offset.Y) * 32f * scale;
                handle.DrawTextureRect(layer.Texture,
                    UIBox2.FromDimensions(canvas.Center + offset - size / 2, size), layer.Color);
            }
        }
        else
        {
            foreach (var part in _parts.Values)
            {
                if (part.Texture != null)
                    handle.DrawTextureRect(part.Texture, canvas, part.Color);
            }
        }

        // Отдельный верхний проход: соседние части и глаза не перекрывают рамку выбора.
        for (var i = 0; i < Regions.Length; i++)
        {
            var region = Regions[i];
            var amount = _highlightAmounts[i];
            if (amount <= 0f || !_parts.ContainsKey(region.Category))
                continue;
            var bounds = UIBox2.FromDimensions(canvas.TopLeft + region.Bounds.TopLeft * scale, region.Bounds.Size * scale);
            var thickness = MathF.Max(1, UIScale);
            var color = Color.InterpolateBetween(palette.Element, palette.Text, 0.25f)
                .WithAlpha(amount);
            handle.DrawRect(bounds, palette.Element.WithAlpha(0.12f * amount * amount));
            handle.DrawRect(UIBox2.FromDimensions(bounds.TopLeft, new(bounds.Width, thickness)), color);
            handle.DrawRect(UIBox2.FromDimensions(new(bounds.Left, bounds.Bottom - thickness), new(bounds.Width, thickness)), color);
            handle.DrawRect(UIBox2.FromDimensions(bounds.TopLeft, new(thickness, bounds.Height)), color);
            handle.DrawRect(UIBox2.FromDimensions(new(bounds.Right - thickness, bounds.Top), new(thickness, bounds.Height)), color);
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!VisibleInTree)
            return;

        var step = args.DeltaSeconds / 0.12f;
        for (var i = 0; i < Regions.Length; i++)
        {
            var region = Regions[i];
            var target = 0f;
            if (_parts.TryGetValue(region.Category, out var part))
                target = part.Uid == SelectedPart ? 1f : region.Category == _hoveredCategory ? 0.8f : 0f;

            var current = _highlightAmounts[i];
            _highlightAmounts[i] = current + Math.Clamp(target - current, -step, step);
        }
    }

    private static bool HasRegion(string category)
    {
        foreach (var region in Regions)
        {
            if (region.Category == category)
                return true;
        }
        return false;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        var category = HitTestRegion(args.RelativePixelPosition, PixelSize);
        if (_hoveredCategory == category)
            return;

        _hoveredCategory = category;
        ToolTip = _hoveredCategory == null ? null :
            _parts.TryGetValue(_hoveredCategory, out var part) ? part.Name : _loc.GetString("fish-surgery-unavailable");
        HoveredPartChanged?.Invoke(ToolTip);
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hoveredCategory = null;
        ToolTip = null;
        HoveredPartChanged?.Invoke(null);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        var category = HitTestRegion(args.RelativePixelPosition, PixelSize);
        if (category == null || !_parts.TryGetValue(category, out var part))
            return;

        args.Handle();
        PartSelected?.Invoke(part.Uid);
    }

    /// <summary>Uses the same source-space regions as drawing at any window size or UI scale.</summary>
    internal static string? HitTestRegion(Vector2 pixelPosition, Vector2 size)
    {
        var canvas = GetCanvas(size);
        var point = (pixelPosition - canvas.TopLeft) / (canvas.Width / 32f);
        foreach (var region in Regions)
        {
            // Полуоткрытые границы не позволяют соседним частям перехватить один и тот же пиксель.
            var bounds = region.Bounds;
            if (point.X >= bounds.Left && point.X < bounds.Right && point.Y >= bounds.Top && point.Y < bounds.Bottom)
                return region.Category;
        }

        return null;
    }

    /// <summary>Centers the shared sprite canvas, cropping only its transparent side margins.</summary>
    internal static UIBox2 GetCanvas(Vector2 size)
    {
        var scale = MathF.Max(1f, MathF.Floor(MathF.Min(size.X / 24f, size.Y / 32f)));
        var extent = new Vector2(32f * scale);
        return UIBox2.FromDimensions((size - extent) / 2f, extent);
    }

    private sealed record Region(string Category, UIBox2 Bounds);
    private sealed record Part(EntityUid Uid, string Name, Texture? Texture, Color Color);
    internal readonly record struct BodyLayer(Texture Texture, Color Color, Vector2 Scale, Vector2 Offset);
}
