using System.Runtime.InteropServices;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;

namespace Content.Client.Shuttles;

public sealed partial class FtlArrivalOverlay
{
    [Dependency] private IConfigurationManager _config = default!;

    private static readonly Color HullColor = Color.FromHex("#87c9e8");
    private static readonly Color WaveColor = Color.FromHex("#d5f4ff");
    private readonly List<WorldRect> _hullMarks = new();

    /// <summary>Добавляет к предупреждению кромку корпуса и сканирующую полосу в локальных координатах шаттла.</summary>
    private void DrawHullSweep(DrawingHandleWorld handle, EntityUid grid, MapGridComponent mapGrid, Box2 localBounds,
        float elapsed)
    {
        if (!_config.GetCVar(FishCVars.ActionParticlesEnabled))
            return;

        var moving = !_config.GetCVar(CCVars.ReducedMotion);
        var hull = mapGrid.LocalAABB;
        var horizontal = hull.Width >= hull.Height;
        var width = horizontal ? hull.Width : hull.Height;
        var margin = mapGrid.TileSize * 0.5f;
        var phase = elapsed * 0.72f % 1;
        var front = (horizontal ? hull.Left : hull.Bottom) - margin + phase * (width + 2 * margin);
        var thickness = mapGrid.TileSize / 16f;
        _hullMarks.Clear();

        var tiles = _maps.GetLocalTilesIntersecting(grid, mapGrid, localBounds);
        while (tiles.MoveNext(out var tile))
        {
            var bounds = _lookups.GetLocalBounds(tile, mapGrid.TileSize);
            var axis = horizontal ? bounds.Center.X : bounds.Center.Y;
            var glow = moving ? Math.Clamp(1f - MathF.Abs(axis - front) / (2 * mapGrid.TileSize), 0, 1) : 0f;
            var color = Color.InterpolateBetween(HullColor, WaveColor, glow).WithAlpha(0.65f + 0.33f * glow);
            var side = tile.GridIndices;
            if (IsHullBoundary(mapGrid, side + (-1, 0)))
                AppendVerticalDashes(bounds.Left, bounds.Bottom, bounds.Height, thickness, color);
            if (IsHullBoundary(mapGrid, side + (1, 0)))
                AppendVerticalDashes(bounds.Right - thickness, bounds.Bottom, bounds.Height, thickness, color);
            if (IsHullBoundary(mapGrid, side + (0, -1)))
                AppendHorizontalDashes(bounds.Bottom, bounds.Left, bounds.Width, thickness, color);
            if (IsHullBoundary(mapGrid, side + (0, 1)))
                AppendHorizontalDashes(bounds.Top - thickness, bounds.Left, bounds.Width, thickness, color);

            if (!moving)
                continue;

            // Полоса пересекает только существующие тайлы, поэтому просветы в корпусе остаются пустыми.
            var start = Math.Max(front - thickness, horizontal ? bounds.Left : bounds.Bottom);
            var end = Math.Min(front + thickness, horizontal ? bounds.Right : bounds.Top);
            if (end <= start)
                continue;

            var scan = horizontal
                ? new Box2(start, bounds.Bottom, end, bounds.Top)
                : new Box2(bounds.Left, start, bounds.Right, end);
            _hullMarks.Add(new WorldRect(scan, WaveColor.WithAlpha(0.5f)));
        }

        handle.DrawRectsUnmodulated(CollectionsMarshal.AsSpan(_hullMarks));
    }

    private bool IsHullBoundary(MapGridComponent grid, Vector2i neighbor)
    {
        return !_maps.TryGetTile(grid, neighbor, out var tile) || tile.IsEmpty;
    }

    private void AppendVerticalDashes(float x, float y, float height, float thickness, Color color)
    {
        _hullMarks.Add(new WorldRect(new Box2(x, y + height * 0.08f, x + thickness, y + height * 0.60f), color));
        _hullMarks.Add(new WorldRect(new Box2(x, y + height * 0.71f, x + thickness, y + height * 0.86f), color));
    }

    private void AppendHorizontalDashes(float y, float x, float width, float thickness, Color color)
    {
        _hullMarks.Add(new WorldRect(new Box2(x + width * 0.08f, y, x + width * 0.60f, y + thickness), color));
        _hullMarks.Add(new WorldRect(new Box2(x + width * 0.71f, y, x + width * 0.86f, y + thickness), color));
    }
}
