using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;

namespace Content.Client._Fish.Graphics.Bloom;

internal sealed partial class FishBloomOverlay
{
    // Сортировка и перекрытие повторяют порядок рисования спрайтов Clyde.
    private void PrepareSprites(IEye eye, Matrix3x2 worldToTarget, Vector2i targetSize, int padding)
    {
        _orderedSprites.Clear();
        foreach (var entity in _sprites)
        {
            var (position, rotation) = _transform.GetWorldPositionRotation(entity.Comp2);
            var bounds = _sprite.CalculateBounds(entity, position, rotation, eye.Rotation);
            var targetBounds = worldToTarget.TransformBox(bounds);
            _orderedSprites.Add(new BloomSprite(entity, position, rotation, targetBounds, bounds.CalcBoundingBox(), HasBloomLayer(entity.Comp1)));
        }

        // Повторяем сортировку Clyde: DrawDepth, RenderOrder, нижний экранный край, EntityUid.
        _orderedSprites.Sort(static (left, right) => CompareDrawOrder(
            left.Entity.Comp.DrawDepth, left.Entity.Comp.RenderOrder, left.Bounds.Top, left.Entity.Owner,
            right.Entity.Comp.DrawDepth, right.Entity.Comp.RenderOrder, right.Bounds.Top, right.Entity.Owner));

        _sourceIndices.Clear();
        var viewportBounds = Box2.FromDimensions(Vector2.Zero, targetSize);
        for (var i = 0; i < _orderedSprites.Count; i++)
        {
            var sprite = _orderedSprites[i];
            if (sprite.Emissive && sprite.Bounds.Enlarged(padding).Intersects(viewportBounds))
                _sourceIndices.Add(i);
        }
    }

    internal static int CompareDrawOrder(
        int leftDepth, uint leftOrder, float leftY, EntityUid leftUid,
        int rightDepth, uint rightOrder, float rightY, EntityUid rightUid)
    {
        var comparison = leftDepth.CompareTo(rightDepth);
        if (comparison != 0)
            return comparison;

        comparison = leftOrder.CompareTo(rightOrder);
        if (comparison != 0)
            return comparison;

        comparison = leftY.CompareTo(rightY);
        return comparison != 0 ? comparison : leftUid.CompareTo(rightUid);
    }

    internal static Vector2i GetBloomTargetSize(Box2 bounds, int padding = TargetPadding)
    {
        // Выравнивание гасит перераспределения буферов при субпиксельном движении камеры.
        var size = bounds.Size + new Vector2(padding * 2 + 2);
        return new Vector2i((int) MathF.Ceiling(size.X / 16f) * 16, (int) MathF.Ceiling(size.Y / 16f) * 16);
    }

    private void DrawOccluders(
        DrawingHandleWorld handle,
        Angle eyeRotation,
        Matrix3x2 worldToSource,
        int blockerStart,
        int blockerCount)
    {
        // Сам источник и всё, что рисуется за ним, не вырезают его свечение.
        for (var i = blockerStart; i < blockerStart + blockerCount; i++)
        {
            var blocker = _orderedSprites[_blockerIndices[i]];
            DrawSpriteLayers(handle, blocker, eyeRotation, worldToSource, false);
        }
    }

    private readonly record struct BloomSprite(
        Entity<SpriteComponent> Entity,
        Vector2 WorldPosition,
        Angle WorldRotation,
        Box2 Bounds,
        Box2 WorldBounds,
        bool Emissive);
}
