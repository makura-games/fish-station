using System.Numerics;
using Content.Client.Graphics;
using Content.Client.Interactable.Components;
using Content.Shared._Fish.Graphics;
using Content.Shared.Light.Components;
using Content.Shared.Stealth.Components;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Fish.Graphics.Bloom;

/// <summary>
/// Adds independently occluded bloom around unshaded sprite layers and emissive tiles.
/// </summary>
internal sealed partial class FishBloomOverlay : Overlay
{
    private const float BrightnessThreshold = 0.10f;
    private const float SoftKnee = 0.05f;
    internal const int TargetPadding = 18;

    private static readonly ProtoId<ShaderPrototype> ExtractShader = "FishBloomExtract";
    private static readonly ProtoId<ShaderPrototype> BlurShader = "FishBloomBlur";
    private static readonly ProtoId<ShaderPrototype> DownsampleShader = "FishBloomDownsample";
    private static readonly ProtoId<ShaderPrototype> CompositeShader = "FishBloomComposite";

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly EntityLookupSystem _lookup;
    private readonly TransformSystem _transform;
    private readonly SharedMapSystem _map;
    private readonly SpriteSystem _sprite;
    private readonly SpriteTreeSystem _spriteTree;
    private readonly EntityQuery<TransformComponent> _transformQuery;
    private readonly EntityQuery<MapGridComponent> _gridQuery;
    private readonly EntityQuery<StealthComponent> _stealthQuery;
    private readonly EntityQuery<InteractionOutlineComponent> _outlineQuery;
    private readonly List<Entity<SpriteComponent, TransformComponent>> _sprites = new();
    private readonly HashSet<Entity<TileEmissionComponent>> _tiles = new();
    private readonly OverlayResourceCache<CachedResources> _resources = new();
    private readonly OverlayResourceCache<CachedResources> _tileResources = new();
    private readonly List<BloomSprite> _orderedSprites = new();
    private readonly List<int> _sourceIndices = new();
    private readonly List<BloomBatchEntry> _batch = new();
    private readonly List<int> _blockerIndices = new();

    private readonly ShaderInstance _extract;
    private readonly ShaderInstance _downsample;
    private readonly ShaderInstance _blurHorizontal;
    private readonly ShaderInstance _blurVertical;
    private readonly ShaderInstance _composite;
    private readonly ShaderInstance _occlude;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public bool Enabled = true;
    public float Strength = 0.4f;
    public FishBloomQuality Quality = FishBloomQuality.Medium;

    public FishBloomOverlay(IEntityManager entityManager)
    {
        IoCManager.InjectDependencies(this);
        _lookup = entityManager.System<EntityLookupSystem>();
        _transform = entityManager.System<TransformSystem>();
        _map = entityManager.System<SharedMapSystem>();
        _sprite = entityManager.System<SpriteSystem>();
        _spriteTree = entityManager.System<SpriteTreeSystem>();
        _transformQuery = entityManager.GetEntityQuery<TransformComponent>();
        _gridQuery = entityManager.GetEntityQuery<MapGridComponent>();
        _stealthQuery = entityManager.GetEntityQuery<StealthComponent>();
        _outlineQuery = entityManager.GetEntityQuery<InteractionOutlineComponent>();

        _extract = _prototype.Index(ExtractShader).InstanceUnique();
        _extract.SetParameter("threshold", BrightnessThreshold);
        _extract.SetParameter("soft_knee", SoftKnee);
        _downsample = _prototype.Index(DownsampleShader).InstanceUnique();
        _blurHorizontal = _prototype.Index(BlurShader).InstanceUnique();
        _blurVertical = _prototype.Index(BlurShader).InstanceUnique();
        _composite = _prototype.Index(CompositeShader).InstanceUnique();
        _occlude = _prototype.Index(SpriteSystem.UnshadedId).Instance();

        ZIndex = -100; // Плашки действий и статусные значки должны рисоваться поверх свечения.
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!Enabled || Strength <= 0f || args.Viewport.Eye == null || args.MapId == MapId.Nullspace)
            return false;

        _sprites.Clear();
        _tiles.Clear();
        var bounds = args.WorldAABB.Enlarged(3f);
        _spriteTree.QueryAabb(_sprites, args.MapId, bounds);
        _lookup.GetEntitiesIntersecting(args.MapId, bounds, _tiles, LookupFlags.Uncontained);
        var visibleCount = 0;
        for (var i = 0; i < _sprites.Count; i++)
        {
            var sprite = _sprites[i];
            if (CanDrawSprite(sprite))
                _sprites[visibleCount++] = sprite;
        }
        _sprites.RemoveRange(visibleCount, _sprites.Count - visibleCount);
        _tiles.RemoveWhere(static source => source.Comp.Deleted || source.Comp.Color.A <= 0f);
        if (_tiles.Count != 0)
            return true;

        foreach (var sprite in _sprites)
        {
            if (HasBloomLayer(sprite.Comp1))
                return true;
        }

        return false;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewport = args.Viewport;
        var eye = viewport.Eye;
        if (eye == null)
            return;

        var resources = _resources.GetForViewport(viewport, static _ => new CachedResources());
        var divisor = GetResolutionDivisor(Quality);
        var contentSize = Vector2i.ComponentMax(Vector2i.One, viewport.Size / divisor);
        var handle = args.WorldHandle;
        var oldTransform = handle.GetTransform();
        var oldShader = handle.GetShader();
        var pixelsPerMeter = EyeManager.PixelsPerMeter * viewport.RenderScale / eye.Zoom *
                             ((Vector2) contentSize / (Vector2) viewport.Size);
        var padding = GetTargetPadding(pixelsPerMeter);
        var targetSize = contentSize + new Vector2i(padding * 2, padding * 2);
        var worldToTarget = GetWorldToTargetMatrix(viewport.GetWorldToLocalMatrix(), viewport.Size, contentSize, padding);

        PrepareSprites(eye, worldToTarget, targetSize, padding);
        var localSize = Vector2i.One;
        foreach (var index in _sourceIndices)
        {
            localSize = Vector2i.ComponentMax(localSize, GetBloomTargetSize(_orderedSprites[index].Bounds, padding));
        }
        localSize = Vector2i.ComponentMin(localSize, targetSize);

        try
        {
            if (_tiles.Count != 0)
            {
                var tiles = _tileResources.GetForViewport(viewport, static _ => new CachedResources());
                EnsureTargets(tiles, targetSize, targetSize * divisor);
                _batch.Clear();
                _blockerIndices.Clear();
                AddBatchEntry(-1, worldToTarget, Vector2i.Zero, targetSize);
                DrawBatch(args.RenderHandle, tiles, eye, pixelsPerMeter, divisor, Vector2i.One);
            }

            if (_sourceIndices.Count == 0)
                return;

            if (resources.ViewportSize != viewport.Size || resources.Divisor != divisor)
            {
                resources.AtlasLayout = default;
                resources.ViewportSize = viewport.Size;
                resources.Divisor = divisor;
            }

            // Сохраняем ёмкость при небольших изменениях; смена рамки и уход крупного источника пересчитывают атлас.
            resources.AtlasLayout = GetAtlasLayout(
                _sourceIndices.Count, localSize, divisor, resources.AtlasLayout, padding);
            localSize = resources.AtlasLayout.CellSize;
            var atlasGrid = resources.AtlasLayout.Grid;
            var atlasSize = new Vector2i(localSize.X * atlasGrid.X, localSize.Y * atlasGrid.Y);
            EnsureTargets(resources, atlasSize, atlasSize * divisor);
            var batchCapacity = atlasGrid.X * atlasGrid.Y;
            for (var start = 0; start < _sourceIndices.Count; start += batchCapacity)
            {
                _batch.Clear();
                _blockerIndices.Clear();
                var count = Math.Min(batchCapacity, _sourceIndices.Count - start);
                for (var slot = 0; slot < count; slot++)
                {
                    var index = _sourceIndices[start + slot];
                    var sprite = _orderedSprites[index];
                    var origin = Vector2.Max(Vector2.Zero, new Vector2(
                        MathF.Floor(sprite.Bounds.Left) - padding,
                        MathF.Floor(sprite.Bounds.Bottom) - padding));
                    var atlasOrigin = new Vector2i(slot % atlasGrid.X * localSize.X, slot / atlasGrid.X * localSize.Y);
                    var matrix = worldToTarget * Matrix3x2.CreateTranslation((Vector2) atlasOrigin - origin);
                    AddBatchEntry(index, matrix, atlasOrigin, localSize);
                }

                DrawBatch(args.RenderHandle, resources, eye, pixelsPerMeter, divisor, atlasGrid);
            }
        }
        finally
        {
            handle.UseShader(oldShader);
            handle.SetTransform(oldTransform);
        }
    }

    private void DrawBatch(
        IRenderHandle render,
        CachedResources resources,
        IEye eye,
        Vector2 pixelsPerMeter,
        int divisor,
        Vector2i atlasGrid)
    {
        var handle = render.DrawingHandleWorld;
        var screen = render.DrawingHandleScreen;
        var source = resources.Source!;
        var occlusion = resources.Occlusion!;
        var ping = resources.Ping!;
        var pong = resources.Pong!;
        var targetSize = ping.Size;

        // Маска пропускания хранится в полном разрешении: края предметов не размываются вместе с ореолом.
        handle.RenderInRenderTarget(occlusion, () =>
        {
            DrawBatchLayers(render, eye, divisor, false);
        }, Color.White);

        handle.RenderInRenderTarget(source, () =>
        {
            DrawBatchLayers(render, eye, divisor, true);
        }, Color.Black);

        // Закрытая часть источника не создаёт ореол; после размытия маска применяется ещё раз.
        _downsample.SetParameter("sample_step", GetDownsampleStep(divisor, source.Size));
        _downsample.SetParameter("axis_samples", GetDownsampleAxisSamples(divisor));
        screen.RenderInRenderTarget(ping, () =>
        {
            screen.SetTransform(Matrix3x2.Identity);
            screen.UseShader(_downsample);
            screen.DrawTextureRect(source.Texture, UIBox2.FromDimensions(Vector2.Zero, targetSize));
        }, Color.Black);

        var texelSize = Vector2.One / (Vector2) targetSize;
        DrawBlurPass(screen, ping.Texture, pong, _blurHorizontal, texelSize, Vector2.UnitX,
            CalculateBlurRadius(Quality, pixelsPerMeter.X), atlasGrid);
        DrawBlurPass(screen, pong.Texture, ping, _blurVertical, texelSize, Vector2.UnitY,
            CalculateBlurRadius(Quality, pixelsPerMeter.Y), atlasGrid);

        _composite.SetParameter("bloom_strength", GetBloomStrength(Strength));
        _composite.SetParameter("occlusion_texture", occlusion.Texture);
        screen.UseShader(_composite);
        foreach (var entry in _batch)
        {
            screen.SetTransform(entry.TargetToWorld);
            screen.DrawTextureRectRegion(ping.Texture, entry.Region, entry.Region);
        }
    }

    private static void DrawBlurPass(
        DrawingHandleScreen handle,
        Texture source,
        IRenderTarget destination,
        ShaderInstance shader,
        Vector2 texelSize,
        Vector2 direction,
        float radius,
        Vector2i atlasGrid)
    {
        shader.SetParameter("texel_size", texelSize);
        shader.SetParameter("blur_direction", direction);
        shader.SetParameter("blur_radius", radius);
        shader.SetParameter("atlas_grid", (Vector2) atlasGrid);

        handle.RenderInRenderTarget(destination, () =>
        {
            try
            {
                handle.SetTransform(Matrix3x2.Identity);
                handle.UseShader(shader);
                handle.DrawTextureRect(source, UIBox2.FromDimensions(Vector2.Zero, destination.Size), Color.White);
            }
            finally
            {
                handle.UseShader(null);
                handle.SetTransform(Matrix3x2.Identity);
            }
        }, Color.Transparent);
    }

    /// <summary>
    /// Rejects sprites whose visibility cannot be reproduced from their source layers alone.
    /// The same filter applies to bloom sources and occluders.
    /// </summary>
    internal bool CanDrawSprite(Entity<SpriteComponent> entity)
    {
        var sprite = entity.Comp;
        if (sprite.Deleted || !sprite.Visible || sprite.ContainerOccluded || sprite.Color.A <= 0f ||
            _stealthQuery.TryComp(entity, out var stealth) && stealth.Enabled)
            return false;

        // Не восстанавливаем скрытые постшейдером пиксели. Обычная обводка сохраняет исходный спрайт.
        var postShaders = _sprite.GetPostShaders(sprite);
        if (postShaders.Count == 0)
            return true;

        return postShaders.Count == 1 &&
               _outlineQuery.TryComp(entity, out var outline) &&
               outline.Active &&
               postShaders[0].Id == ContentPostShaderIds.InteractionOutline;
    }

    private static bool HasBloomLayer(SpriteComponent sprite)
    {
        foreach (var layer in sprite.AllLayers)
        {
            if (layer is SpriteComponent.Layer
                {
                    Visible: true,
                    Blank: false,
                    ShaderPrototype: var shader,
                    CopyToShaderParameters: null,
                } && shader == SpriteSystem.UnshadedId)
                return true;
        }

        return false;
    }

    private void DrawSpriteLayers(
        DrawingHandleWorld handle,
        BloomSprite source,
        Angle eyeRotation,
        Matrix3x2 worldToTarget,
        bool emissive)
    {
        var hasEmission = false;
        foreach (var spriteLayer in source.Entity.Comp.AllLayers)
        {
            if (spriteLayer is not SpriteComponent.Layer
                {
                    Visible: true,
                    Blank: false,
                    ShaderPrototype: var shader,
                    CopyToShaderParameters: null,
                } layer)
                continue;

            if (emissive && shader == SpriteSystem.UnshadedId)
            {
                // Как в Clyde, верхний слой сначала закрывает нижний с учётом своей прозрачности.
                if (hasEmission)
                    DrawLayer(handle, source.Entity.Comp, layer, source.WorldPosition, source.WorldRotation, eyeRotation, worldToTarget, false);

                DrawLayer(handle, source.Entity.Comp, layer, source.WorldPosition, source.WorldRotation, eyeRotation, worldToTarget, true);
                hasEmission = true;
            }
            else if (!emissive || hasEmission)
            {
                // Верхний непрозрачный слой самого спрайта тоже закрывает свечение нижних слоёв.
                DrawLayer(handle, source.Entity.Comp, layer, source.WorldPosition, source.WorldRotation, eyeRotation, worldToTarget, false);
            }
        }
    }

    private void DrawLayer(
        DrawingHandleWorld handle,
        SpriteComponent sprite,
        SpriteComponent.Layer layer,
        Vector2 worldPosition,
        Angle worldRotation,
        Angle eyeRotation,
        Matrix3x2 worldToTarget,
        bool emissive)
    {
        var angle = (worldRotation + eyeRotation).Reduced().FlipPositive();
        var state = layer.ActualState;
        var direction = state == null
            ? RsiDirection.South
            : SpriteComponent.Layer.GetDirection(state.RsiDirections, angle);
        layer.GetLayerDrawMatrix(direction, out var layerMatrix);

        if (sprite.EnableDirectionOverride && state != null)
            direction = sprite.DirectionOverride.Convert(state.RsiDirections);

        direction = direction.OffsetRsiDir(layer.DirOffset);
        var texture = state?.GetFrame(direction, layer.AnimationFrame) ?? layer.Texture;
        if (texture == null)
            return;

        var cardinal = !sprite.NoRotation && sprite.SnapCardinals
            ? angle.RoundToCardinalAngle()
            : Angle.Zero;
        var renderRotation = sprite.NoRotation ? -eyeRotation : worldRotation - cardinal;
        if (sprite.GranularLayersRendering)
        {
            renderRotation = layer.RenderingStrategy switch
            {
                LayerRenderingStrategy.Default => worldRotation,
                LayerRenderingStrategy.NoRotation => -eyeRotation,
                LayerRenderingStrategy.SnapToCardinals => worldRotation - angle.RoundToCardinalAngle(),
                _ => renderRotation,
            };
        }

        var color = sprite.Color * layer.Color;
        if (color.A <= 0f || emissive && GetBrightness(color) <= 0f)
            return;

        if (!emissive)
            color = new Color(0f, 0f, 0f, color.A);

        // Цвет хранится в вершинах команды, а не в общем изменяемом uniform.
        handle.UseShader(emissive ? _extract : _occlude);
        handle.SetTransform(GetSourceToTargetMatrix(
            layerMatrix,
            sprite.LocalMatrix,
            worldPosition,
            renderRotation,
            worldToTarget));
        handle.DrawTextureRect(
            texture,
            Box2.CenteredAround(Vector2.Zero, (Vector2) texture.Size / EyeManager.PixelsPerMeter),
            color);
    }

    internal static Matrix3x2 GetSourceToTargetMatrix(
        Matrix3x2 layerMatrix,
        Matrix3x2 spriteMatrix,
        Vector2 worldPosition,
        Angle worldRotation,
        Matrix3x2 worldToTarget)
    {
        return layerMatrix * spriteMatrix * Matrix3Helpers.CreateTransform(worldPosition, worldRotation) * worldToTarget;
    }

    /// <summary>Maps normalized strength to a perceptual bloom intensity capped at 2.5.</summary>
    internal static float GetBloomStrength(float strength)
    {
        if (!float.IsFinite(strength) || strength <= 0f)
            return 0f;

        var value = Math.Clamp(strength, 0f, 1f);
        return value * (2.5f + 2.5f * value) * 0.5f;
    }

    internal static int GetResolutionDivisor(FishBloomQuality quality)
    {
        return quality switch
        {
            FishBloomQuality.Low => 8,
            FishBloomQuality.High => 2,
            _ => 4,
        };
    }

    internal static int GetDownsampleAxisSamples(int divisor)
    {
        return divisor == 8 ? 4 : 2;
    }

    internal static Vector2 GetDownsampleStep(int divisor, Vector2 sourceSize)
    {
        return new Vector2(divisor == 8 ? 1f : divisor * 0.25f) / sourceSize;
    }

    internal static int GetTargetPadding(Vector2 pixelsPerMeter)
    {
        // По два texel за радиусом ядра покрывают bilinear-фильтрацию без огромной пустой рамки на Low.
        var radius = Math.Max(CalculateBlurRadius(FishBloomQuality.Medium, pixelsPerMeter.X),
            CalculateBlurRadius(FishBloomQuality.Medium, pixelsPerMeter.Y));
        return (int) MathF.Ceiling(radius) + 2;
    }

    internal static Matrix3x2 GetWorldToTargetMatrix(
        Matrix3x2 worldToViewport,
        Vector2 viewportSize,
        Vector2 contentSize,
        int padding = TargetPadding)
    {
        return worldToViewport * Matrix3x2.CreateScale(contentSize / viewportSize) *
               Matrix3x2.CreateTranslation(padding, padding);
    }

    internal static float CalculateBlurRadius(FishBloomQuality quality, float pixelsPerMeter)
    {
        // Качество меняет разрешение маски, но не мировой радиус ореола.
        return Math.Clamp(0.22f * pixelsPerMeter, 0.5f, 16f);
    }

    private void DrawTileEmission(DrawingHandleWorld handle, Matrix3x2 worldToTarget)
    {
        foreach (var source in _tiles)
        {
            if (!_transformQuery.TryComp(source, out var xform) ||
                xform.GridUid is not { } gridUid || !_gridQuery.TryComp(gridUid, out var grid))
                continue;

            var tile = _map.LocalToTile(gridUid, grid, xform.Coordinates);
            var matrix = _transform.GetWorldMatrix(gridUid);
            // Как у плиток Clyde, округление выполняется шейдером для каждой вершины, а не для начала сетки.
            handle.SetTransform(matrix * worldToTarget);
            handle.UseShader(_extract);
            handle.DrawRect(_lookup.GetLocalBounds(tile, grid.TileSize), source.Comp.Color);
        }
    }

    private static float GetBrightness(Color color)
    {
        return Math.Max(color.R, Math.Max(color.G, color.B));
    }

    private void EnsureTargets(CachedResources resources, Vector2i size, Vector2i sourceSize)
    {
        if (resources.Ping?.Texture.Size == size && resources.Pong?.Texture.Size == size && resources.Source?.Size == sourceSize)
            return;

        resources.Dispose();
        var format = new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb);
        var samples = new TextureSampleParameters { Filter = true };
        resources.Source = _clyde.CreateRenderTarget(sourceSize, format, samples, "fish-bloom-source");
        resources.Occlusion = _clyde.CreateRenderTarget(sourceSize, format, new TextureSampleParameters { Filter = false }, "fish-bloom-occlusion");
        resources.Ping = _clyde.CreateRenderTarget(size, format, samples, "fish-bloom-ping");
        resources.Pong = _clyde.CreateRenderTarget(size, format, samples, "fish-bloom-pong");
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();
        _tileResources.Dispose();
        _extract.Dispose();
        _downsample.Dispose();
        _blurHorizontal.Dispose();
        _blurVertical.Dispose();
        _composite.Dispose();
        base.DisposeBehavior();
    }

    private sealed class CachedResources : IDisposable
    {
        public IRenderTexture? Source;
        public IRenderTexture? Occlusion;
        public IRenderTexture? Ping;
        public IRenderTexture? Pong;
        public Vector2i ViewportSize;
        public int Divisor;
        public BloomAtlasLayout AtlasLayout;

        public void Dispose()
        {
            Source?.Dispose();
            Source = null;
            Occlusion?.Dispose();
            Occlusion = null;
            Ping?.Dispose();
            Pong?.Dispose();
            Ping = null;
            Pong = null;
        }
    }
}

internal enum FishBloomQuality : byte
{
    Low,
    Medium,
    High,
}

internal static class FishBloomQualityExtensions
{
    public static FishBloomQuality Parse(string quality)
    {
        return quality switch
        {
            FishBloomCVars.QualityLow => FishBloomQuality.Low,
            FishBloomCVars.QualityHigh => FishBloomQuality.High,
            _ => FishBloomQuality.Medium,
        };
    }
}
