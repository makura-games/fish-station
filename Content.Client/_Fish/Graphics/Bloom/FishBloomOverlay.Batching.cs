using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;

namespace Content.Client._Fish.Graphics.Bloom;

internal sealed partial class FishBloomOverlay
{
    // Несколько независимых источников используют общие проходы, не смешивая свои маски перекрытия.
    private const int AtlasSourceSize = 1024;

    /// <summary>
    /// Retains atlas capacity until cells need to grow, without exceeding the source texture budget.
    /// Pass an empty layout when the viewport resolution or quality changes.
    /// </summary>
    internal static BloomAtlasLayout GetAtlasLayout(
        int sourceCount,
        Vector2i requiredCellSize,
        int divisor,
        BloomAtlasLayout previous)
    {
        var cellSize = Vector2i.ComponentMax(requiredCellSize, previous.CellSize);
        var requiredGrid = GetAtlasGrid(sourceCount, cellSize, divisor);
        var maxGrid = new Vector2i(
            Math.Max(1, AtlasSourceSize / divisor / cellSize.X),
            Math.Max(1, AtlasSourceSize / divisor / cellSize.Y));
        var grid = Vector2i.ComponentMin(Vector2i.ComponentMax(requiredGrid, previous.Grid), maxGrid);
        return new BloomAtlasLayout(cellSize, grid);
    }

    internal readonly record struct BloomAtlasLayout(Vector2i CellSize, Vector2i Grid);

    internal static Vector2i GetAtlasGrid(int sourceCount, Vector2i cellSize, int divisor)
    {
        var maxColumns = Math.Max(1, AtlasSourceSize / divisor / cellSize.X);
        var maxRows = Math.Max(1, AtlasSourceSize / divisor / cellSize.Y);
        var columns = Math.Clamp((int) MathF.Ceiling(MathF.Sqrt(sourceCount)), 1, maxColumns);
        var rows = Math.Clamp((sourceCount + columns - 1) / columns, 1, maxRows);
        return new Vector2i(columns, rows);
    }

    private void AddBatchEntry(int sourceIndex, Matrix3x2 matrix, Vector2i origin, Vector2i size)
    {
        if (!Matrix3x2.Invert(matrix, out var inverse))
            return;

        var region = UIBox2i.FromDimensions(origin, size);
        var worldBounds = inverse.TransformBox(Box2.FromDimensions(origin, size));
        var blockerStart = _blockerIndices.Count;
        // Обе маски используют одинаковые перекрытия; находим их один раз до рисования.
        for (var i = sourceIndex + 1; i < _orderedSprites.Count; i++)
        {
            if (_orderedSprites[i].WorldBounds.Intersects(worldBounds))
                _blockerIndices.Add(i);
        }
        _batch.Add(new BloomBatchEntry(sourceIndex, matrix, inverse, region,
            blockerStart, _blockerIndices.Count - blockerStart));
    }

    private void DrawBatchLayers(IRenderHandle render, IEye eye, int divisor, bool emissive)
    {
        var handle = render.DrawingHandleWorld;
        try
        {
            foreach (var entry in _batch)
            {
                // Большой перекрывающий спрайт не должен закрашивать соседнюю ячейку атласа.
                render.SetScissor(new UIBox2i(
                    entry.Region.Left * divisor, entry.Region.Top * divisor,
                    entry.Region.Right * divisor, entry.Region.Bottom * divisor));
                var matrix = entry.WorldToTarget * Matrix3x2.CreateScale(divisor);
                if (emissive)
                {
                    if (entry.SourceIndex < 0)
                        DrawTileEmission(handle, matrix);
                    else
                        DrawSpriteLayers(handle, _orderedSprites[entry.SourceIndex], eye.Rotation, matrix, true);
                }

                DrawOccluders(handle, eye.Rotation, matrix, entry.BlockerStart, entry.BlockerCount);
            }
        }
        finally
        {
            render.SetScissor(null);
        }
    }

    private readonly record struct BloomBatchEntry(
        int SourceIndex,
        Matrix3x2 WorldToTarget,
        Matrix3x2 TargetToWorld,
        UIBox2i Region,
        int BlockerStart,
        int BlockerCount);
}
