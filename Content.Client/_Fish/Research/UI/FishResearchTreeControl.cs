using System.Numerics;
using Content.Client.Stylesheets.Palette;
using Content.Shared.Research.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;

namespace Content.Client._Fish.Research.UI;

/// <summary>
/// A clipped technology graph. Layout and input use UI units; rendering uses local physical pixels.
/// </summary>
public sealed partial class FishResearchTreeControl : Control
{
    private static readonly Vector2 NodeSize = new(252, 124);
    private static readonly Color ResearchedTextColor = Color.InterpolateBetween(Palettes.Green.Base, Color.White, 0.35f);
    private static readonly Color AvailableTextColor = Color.InterpolateBetween(Palettes.Navy.Base, Color.White, 0.55f);
    private static readonly Color LockedTextColor = Color.InterpolateBetween(Palettes.Slate.Base, Color.White, 0.45f);
    private const float ColumnSpacing = 308;
    private const float RowSpacing = 146;
    private readonly Dictionary<ProtoId<TechnologyPrototype>, Node> _nodes = new();
    private readonly Dictionary<ProtoId<TechnologyPrototype>, UIBox2> _nodeBounds = new();
    private readonly List<FishResearchTreeEdge> _edges = new();
    private readonly Dictionary<ProtoId<TechDisciplinePrototype>, string> _disciplineNames = new();
    private readonly List<(Vector2 Position, string Name, Color Color)> _lanes = new();
    private readonly Dictionary<ProtoId<TechnologyPrototype>, FishResearchNodeState> _states = new();
    private readonly Dictionary<FishResearchNodeState, string> _stateNames = new();
    private readonly StyleBoxFlat _nodeBox = new();
    private readonly HashSet<ProtoId<TechnologyPrototype>> _visible = new();
    private FishResearchTreeCatalog? _catalog;
    private ProtoId<TechDisciplinePrototype>? _discipline;
    private string _search = string.Empty;
    private bool _dragging;
    private float _dragDistance;
    private Vector2 _pressPosition;
    private ProtoId<TechnologyPrototype>? _pressedTechnology;
    private ProtoId<TechnologyPrototype>? _hoveredTechnology;
    private bool _captionsDirty = true;
    private bool _centerWhenSized;

    /// <summary>Raised when a node is selected, without purchasing it.</summary>
    public event Action<ProtoId<TechnologyPrototype>>? OnTechnologySelected;

    /// <summary>Raised after the graph viewport or its visible subset changes.</summary>
    public event Action? OnViewChanged;

    /// <summary>Technologies currently shown, including prerequisites retained by a filter.</summary>
    public IReadOnlyCollection<ProtoId<TechnologyPrototype>> VisibleTechnologyIds => _visible;

    /// <summary>Scale relative to the graph's UI coordinates.</summary>
    public float Zoom { get; private set; } = 0.8f;

    /// <summary>Translation in UI units.</summary>
    public Vector2 Pan { get; private set; } = new(20, 20);

    /// <summary>The selected technology, which may lie outside the current filter.</summary>
    public ProtoId<TechnologyPrototype>? SelectedTechnology { get; private set; }

    /// <summary>Cached visible card bounds in graph coordinates, shared by rendering, routing and hit testing.</summary>
    internal IReadOnlyDictionary<ProtoId<TechnologyPrototype>, UIBox2> NodeBounds => _nodeBounds;

    /// <summary>Cached paths consumed directly by the renderer.</summary>
    internal IReadOnlyList<FishResearchTreeEdge> Edges => _edges;

    public FishResearchTreeControl()
    {
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
    }

    internal void SetCatalog(FishResearchTreeCatalog catalog, ILocalizationManager loc)
    {
        _catalog = catalog;
        foreach (var state in Enum.GetValues<FishResearchNodeState>())
            _stateNames[state] = loc.GetString($"fish-research-tree-state-{state.ToString().ToLowerInvariant()}");
        _disciplineNames.Clear();
        foreach (var (id, discipline) in catalog.Disciplines)
            _disciplineNames.Add(id, loc.GetString(discipline.Name));

        if (SelectedTechnology is { } selected && !catalog.Technologies.ContainsKey(selected))
            SelectedTechnology = null;

        RebuildVisibility();
        RebuildLayout();
    }

    internal void UpdateStates(IReadOnlyDictionary<ProtoId<TechnologyPrototype>, FishResearchNodeState> states)
    {
        _states.Clear();
        foreach (var (id, state) in states)
            _states.Add(id, state);
    }

    /// <summary>Restores or changes a viewport without rebuilding the catalog or layout.</summary>
    public void SetViewport(float zoom, Vector2 pan)
    {
        if (!float.IsFinite(zoom) || !float.IsFinite(pan.X) || !float.IsFinite(pan.Y))
            return;

        Zoom = Math.Clamp(zoom, 0.35f, 1.6f);
        Pan = pan;
        _centerWhenSized = false;
        _captionsDirty = true;
        OnViewChanged?.Invoke();
    }

    /// <summary>Shows matching technologies together with their transitive prerequisites.</summary>
    public void SetFilter(ProtoId<TechDisciplinePrototype>? discipline, string search)
    {
        var trimmed = search.Trim();
        if (_discipline == discipline && _search == trimmed)
            return;
        _discipline = discipline;
        _search = trimmed;
        RebuildVisibility();
        RebuildLayout();
    }

    /// <summary>Selects a catalog entry. Selection never sends a purchase request.</summary>
    public bool SelectTechnology(ProtoId<TechnologyPrototype> id)
    {
        if (_catalog == null || !_catalog.Technologies.ContainsKey(id))
            return false;

        SelectedTechnology = id;
        OnTechnologySelected?.Invoke(id);
        return true;
    }

    /// <summary>Moves the selected visible node to the center of the viewport.</summary>
    public void CenterSelection()
    {
        if (SelectedTechnology is not { } selected || !_nodes.TryGetValue(selected, out var node) || !_visible.Contains(selected))
            return;

        if (Size.X <= 0 || Size.Y <= 0)
        {
            _centerWhenSized = true;
            return;
        }

        SetViewport(Zoom, Size / 2 - (node.Position + NodeSize / 2) * Zoom);
    }

    /// <summary>Changes scale while keeping an anchor fixed in UI coordinates.</summary>
    public void ZoomAt(float factor, Vector2 anchor)
    {
        var graphAnchor = UiToGraph(anchor);
        var zoom = Math.Clamp(Zoom * factor, 0.35f, 1.6f);
        SetViewport(zoom, anchor - graphAnchor * zoom);
    }

    private Vector2 GraphToUi(Vector2 position) => position * Zoom + Pan;
    private Vector2 UiToGraph(Vector2 position) => (position - Pan) / Zoom;

    private void RebuildVisibility()
    {
        _visible.Clear();
        if (_catalog == null)
            return;

        var pending = new Stack<ProtoId<TechnologyPrototype>>();
        foreach (var (id, technology) in _catalog.Technologies)
        {
            if (_discipline != null && technology.Prototype.Discipline != _discipline)
                continue;
            if (_search.Length != 0 && !technology.SearchText.Contains(_search, StringComparison.CurrentCultureIgnoreCase))
                continue;
            pending.Push(id);
        }

        // Fish-edit: внешний prerequisite остаётся видимым вместе с междисциплинарным ребром.
        while (pending.TryPop(out var id))
        {
            if (!_visible.Add(id) || !_catalog.Technologies.TryGetValue(id, out var technology))
                continue;
            foreach (var prerequisite in technology.Prototype.TechnologyPrerequisites)
            {
                if (_catalog.Technologies.ContainsKey(prerequisite))
                    pending.Push(prerequisite);
            }
        }
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick && args.Function != EngineKeyFunctions.UIRightClick)
            return;

        _dragging = true;
        _dragDistance = 0;
        _pressPosition = args.RelativePosition;
        _pressedTechnology = args.Function == EngineKeyFunctions.UIClick ? HitTest(args.RelativePosition) : null;
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick && args.Function != EngineKeyFunctions.UIRightClick)
            return;

        if (_dragging && _dragDistance < 5 && Vector2.Distance(_pressPosition, args.RelativePosition) < 5 &&
            SizeBox.Contains(args.RelativePosition) && _pressedTechnology is { } selected && HitTest(args.RelativePosition) == selected)
            SelectTechnology(selected);

        _dragging = false;
        _pressedTechnology = null;
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (_dragging)
        {
            _dragDistance += args.Relative.Length();
            Pan += args.Relative;
            OnViewChanged?.Invoke();
        }

        _hoveredTechnology = HitTest(args.RelativePosition);
        ToolTip = _hoveredTechnology is { } id && _nodes.TryGetValue(id, out var node) ? node.Technology.Name : null;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hoveredTechnology = null;
        ToolTip = null;
    }

    protected override void ControlFocusExited()
    {
        base.ControlFocusExited();
        _dragging = false;
        _pressedTechnology = null;
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);
        ZoomAt(MathF.Pow(1.12f, args.Delta.Y), args.RelativePosition);
        args.Handle();
    }

    private ProtoId<TechnologyPrototype>? HitTest(Vector2 position)
    {
        if (!SizeBox.Contains(position))
            return null;
        var graphPosition = UiToGraph(position);
        foreach (var id in _visible)
        {
            if (_nodeBounds.TryGetValue(id, out var bounds) && bounds.Contains(graphPosition))
                return id;
        }
        return null;
    }

    protected override void StylePropertiesChanged()
    {
        base.StylePropertiesChanged();
        _captionsDirty = true;
    }

    protected override void UIScaleChanged()
    {
        base.UIScaleChanged();
        _captionsDirty = true;
    }

    protected override void Resized()
    {
        base.Resized();
        if (_centerWhenSized)
            CenterSelection();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        handle.DrawRect(PixelSizeBox, Palettes.Slate.BackgroundDark);
        var font = StylePropertyDefault(Label.StylePropertyFont, UserInterfaceManager.ThemeDefaults.LabelFont);
        if (_captionsDirty)
        {
            foreach (var node in _nodes.Values)
                node.Caption = WrapCaption(handle, font, node.Technology.Name, 184);
            _captionsDirty = false;
        }

        DrawGrid(handle);
        foreach (var (position, name, color) in _lanes)
            DrawText(handle, font, position, name, color);

        // Fish-start: выделенные связи и стрелки рисуются поверх общих серых участков.
        DrawConnections(handle, highlighted: false);
        DrawConnections(handle, highlighted: true);
        // Fish-end

        foreach (var id in _visible)
        {
            var node = _nodes[id];
            var bounds = _nodeBounds[id];
            var rect = ToPixels(bounds.TopLeft, bounds.Size);
            if (!rect.Intersects(PixelSizeBox))
                continue;

            var state = _states.GetValueOrDefault(id);
            var statusColor = StateColor(state);
            var selected = SelectedTechnology == id;
            var border = selected ? Palettes.Gold.Base : _hoveredTechnology == id ? node.Color : statusColor;
            _nodeBox.BackgroundColor = selected ? Palettes.Navy.BackgroundLight : Palettes.Navy.BackgroundDark;
            _nodeBox.BorderColor = border;
            _nodeBox.BorderThickness = new Thickness(selected ? 2 : 1);
            _nodeBox.Draw(handle, rect, UIScale * Zoom);
            handle.DrawRect(ToPixels(node.Position, new Vector2(NodeSize.X, 4)), node.Color);
            handle.DrawTextureRect(node.Technology.Icon, ToPixels(node.Position + new Vector2(12, 16), new Vector2(32)), Color.White);
            DrawText(handle, font, node.Position + new Vector2(54, 14), node.Caption, Color.White);
            DrawText(handle, font, node.Position + new Vector2(12, 72), node.Technology.Metadata, Palettes.Gold.Base);
            if (_stateNames.TryGetValue(state, out var stateName))
                DrawText(handle, font, node.Position + new Vector2(12, 98), stateName, statusColor);
        }
    }

    private void DrawGrid(DrawingHandleScreen handle)
    {
        var step = 32 * Zoom * UIScale;
        var offset = Pan * UIScale;
        for (var x = offset.X % step; x < PixelWidth; x += step)
        for (var y = offset.Y % step; y < PixelHeight; y += step)
            handle.DrawRect(UIBox2.FromDimensions(new Vector2(x, y), new Vector2(1)), Palettes.Slate.BackgroundLight);
    }

    private void DrawConnections(DrawingHandleScreen handle, bool highlighted)
    {
        foreach (var edge in _edges)
        {
            var active = SelectedTechnology == edge.From || SelectedTechnology == edge.To;
            if (active != highlighted)
                continue;

            var color = active ? _nodes[edge.To].Color : Palettes.Slate.HoveredElement;
            DrawConnection(handle, edge.Points, color);
        }
    }

    private void DrawConnection(DrawingHandleScreen handle, IReadOnlyList<Vector2> points, Color color)
    {
        for (var i = 1; i < points.Count; i++)
            Line(points[i - 1], points[i]);
        var to = points[^1];
        Line(to + new Vector2(-7, -4), to);
        Line(to + new Vector2(-7, 4), to);
        return;

        void Line(Vector2 a, Vector2 b) => handle.DrawLine(GraphToUi(a) * UIScale, GraphToUi(b) * UIScale, color);
    }

    private UIBox2 ToPixels(Vector2 position, Vector2 size) => UIBox2.FromDimensions(GraphToUi(position) * UIScale, size * Zoom * UIScale);

    private void DrawText(DrawingHandleScreen handle, Font font, Vector2 position, string text, Color color)
    {
        handle.DrawString(font, GraphToUi(position) * UIScale, text.AsSpan(), UIScale * Zoom, color);
    }

    private string WrapCaption(DrawingHandleScreen handle, Font font, string text, float width)
    {
        var scale = UIScale * Zoom;
        var limit = width * scale;
        if (handle.GetDimensions(font, text.AsSpan(), scale).X <= limit)
            return text;

        var split = text.Length;
        while (split > 0 && handle.GetDimensions(font, text.AsSpan(0, split), scale).X > limit)
            split--;
        var space = text.LastIndexOf(' ', Math.Max(0, split - 1), split);
        if (space > 0)
            split = space;
        var first = text[..split].TrimEnd();
        var rest = text[split..].TrimStart();
        if (handle.GetDimensions(font, rest.AsSpan(), scale).X <= limit)
            return first + "\n" + rest;
        var end = rest.Length;
        while (end > 0 && handle.GetDimensions(font, (rest[..end] + "…").AsSpan(), scale).X > limit)
            end--;
        var second = end == rest.Length ? rest : rest[..end].TrimEnd() + "…";
        return first + "\n" + second;
    }

    internal static Color StateColor(FishResearchNodeState state) => state switch
    {
        FishResearchNodeState.Researched => ResearchedTextColor,
        FishResearchNodeState.Available => AvailableTextColor,
        _ => LockedTextColor,
    };

    private sealed class Node(FishResearchTechnology technology, Vector2 position, Color color)
    {
        public readonly FishResearchTechnology Technology = technology;
        public readonly Vector2 Position = position;
        public readonly Color Color = color;
        public string Caption = string.Empty;
    }
}
