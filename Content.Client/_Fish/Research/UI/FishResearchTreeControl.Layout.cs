using System.Linq;
using System.Numerics;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Client._Fish.Research.UI;

public sealed partial class FishResearchTreeControl
{
    private const float RoutingClearance = 10;

    // Fish-edit: фильтр пересобирает только видимый подграф; обновления очков сюда не заходят.
    private void RebuildLayout()
    {
        _nodes.Clear();
        _nodeBounds.Clear();
        _edges.Clear();
        _lanes.Clear();
        if (_catalog == null)
            return;

        var layers = new Dictionary<ProtoId<TechnologyPrototype>, int>();
        foreach (var technology in _visible)
            GetLayer(technology, layers, new HashSet<ProtoId<TechnologyPrototype>>());

        // Fish-edit: пустые ранги удаляются без изменения порядка зависимых технологий.
        var compactLayers = layers.Values.Distinct().Order().Select((layer, column) => (layer, column))
            .ToDictionary(pair => pair.layer, pair => pair.column);
        var laneY = 24f;
        foreach (var (disciplineId, discipline) in _catalog.Disciplines)
        {
            var technologies = _catalog.Technologies
                .Where(pair => _visible.Contains(pair.Key) && pair.Value.Prototype.Discipline == disciplineId)
                .OrderBy(pair => pair.Value.Prototype.Tier)
                .ThenBy(pair => pair.Value.Name, StringComparer.CurrentCulture)
                .ToArray();
            if (technologies.Length == 0)
                continue;

            _lanes.Add((new Vector2(24, laneY), _disciplineNames[disciplineId], discipline.Color));
            var rows = new Dictionary<int, int>();
            foreach (var (id, technology) in technologies)
            {
                var column = compactLayers[layers[id]];
                rows.TryGetValue(column, out var row);
                rows[column] = row + 1;
                var position = new Vector2(24 + column * ColumnSpacing, laneY + 34 + row * RowSpacing);
                _nodes.Add(id, new Node(technology, position, discipline.Color));
                _nodeBounds.Add(id, UIBox2.FromDimensions(position, NodeSize));
            }

            laneY += 54 + rows.Values.Max() * RowSpacing;
        }

        foreach (var (id, node) in _nodes)
        foreach (var prerequisite in node.Technology.Prototype.TechnologyPrerequisites)
        {
            if (!_nodeBounds.TryGetValue(prerequisite, out var from))
                continue;
            _edges.Add(new FishResearchTreeEdge(prerequisite, id, RouteEdge(from, _nodeBounds[id])));
        }

        _captionsDirty = true;
        _hoveredTechnology = null;
        ToolTip = null;
        OnViewChanged?.Invoke();
    }

    private int GetLayer(ProtoId<TechnologyPrototype> id, Dictionary<ProtoId<TechnologyPrototype>, int> layers, HashSet<ProtoId<TechnologyPrototype>> visiting)
    {
        if (layers.TryGetValue(id, out var cached))
            return cached;
        if (!_visible.Contains(id) || _catalog == null || !_catalog.Technologies.TryGetValue(id, out var technology))
            return -1;
        if (!visiting.Add(id))
            return technology.Prototype.Tier - 1;

        // Fish-edit: один tier может содержать несколько последовательных зависимостей.
        var layer = Math.Max(0, technology.Prototype.Tier - 1);
        foreach (var prerequisite in technology.Prototype.TechnologyPrerequisites)
            layer = Math.Max(layer, GetLayer(prerequisite, layers, visiting) + 1);

        visiting.Remove(id);
        layers[id] = layer;
        return layer;
    }

    private IReadOnlyList<Vector2> RouteEdge(UIBox2 source, UIBox2 destination)
    {
        var from = new Vector2(source.Right, source.Center.Y);
        var to = new Vector2(destination.Left, destination.Center.Y);
        var gutter = (ColumnSpacing - NodeSize.X) / 2;
        var sourceX = from.X + gutter;
        var destinationX = to.X - gutter;

        // Fish-edit: вертикали проходят только в межколоночных промежутках, где нет карточек.
        if (MathF.Abs(sourceX - destinationX) < 0.01f)
            return new[] { from, new Vector2(sourceX, from.Y), new Vector2(destinationX, to.Y), to };

        var left = MathF.Min(sourceX, destinationX);
        var right = MathF.Max(sourceX, destinationX);
        var blockers = new List<UIBox2>();
        var corridors = new HashSet<float> { from.Y, to.Y };
        foreach (var bounds in _nodeBounds.Values)
        {
            if (bounds.Right + RoutingClearance <= left || bounds.Left - RoutingClearance >= right)
                continue;
            blockers.Add(bounds);
            corridors.Add(bounds.Top - RoutingClearance);
            corridors.Add(bounds.Bottom + RoutingClearance);
        }

        var bestY = from.Y;
        var bestLength = float.PositiveInfinity;
        var middleY = (from.Y + to.Y) / 2;
        foreach (var corridor in corridors)
        {
            var blocked = false;
            foreach (var bounds in blockers)
            {
                if (corridor <= bounds.Top - RoutingClearance || corridor >= bounds.Bottom + RoutingClearance)
                    continue;
                blocked = true;
                break;
            }
            if (blocked)
                continue;

            var length = MathF.Abs(corridor - from.Y) + MathF.Abs(corridor - to.Y);
            if (length > bestLength || length == bestLength && MathF.Abs(corridor - middleY) >= MathF.Abs(bestY - middleY))
                continue;
            bestLength = length;
            bestY = corridor;
        }

        // Fish-edit: верхняя и нижняя границы blockers гарантируют существование свободного коридора.
        return new[]
        {
            from,
            new Vector2(sourceX, from.Y),
            new Vector2(sourceX, bestY),
            new Vector2(destinationX, bestY),
            new Vector2(destinationX, to.Y),
            to,
        };
    }
}

/// <summary>A directed prerequisite path in graph coordinates, cached until the visible layout changes.</summary>
internal sealed record FishResearchTreeEdge(
    ProtoId<TechnologyPrototype> From,
    ProtoId<TechnologyPrototype> To,
    IReadOnlyList<Vector2> Points);
