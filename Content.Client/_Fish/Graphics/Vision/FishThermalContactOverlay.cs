using System.Numerics;
using Content.Client.Graphics;
using Content.Client.Interactable.Components;
using Content.Shared._Fish.Vision;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Fish.Graphics.Vision;

/// <summary>Renders live PVS sprites through walls, colored by server-measured temperature.</summary>
internal sealed partial class FishThermalContactOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "FishThermalContact";
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;
    private readonly FishVisionSystem _vision;
    private readonly IEntityManager _entityManager;
    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;
    private readonly SharedContainerSystem _container;
    private readonly ShaderInstance _shader;
    private readonly List<Contact> _contacts = new();
    private readonly OverlayResourceCache<Target> _resources = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public FishThermalContactOverlay(FishVisionSystem vision, IEntityManager entityManager)
    {
        IoCManager.InjectDependencies(this);
        _vision = vision;
        _entityManager = entityManager;
        _sprite = entityManager.System<SpriteSystem>();
        _transform = entityManager.System<TransformSystem>();
        _container = entityManager.System<SharedContainerSystem>();
        _shader = _prototype.Index(Shader).InstanceUnique();
        ZIndex = 301;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args) =>
        _vision.ThermalViewer != null && _vision.ThermalRange > 0f &&
        args.Viewport.Eye != null && args.Viewport.Eye == _eye.CurrentEye;

    /// <summary>Rejects detached, contained or otherwise hidden sprite contacts; cloaking does not hide body heat.</summary>
    internal bool CanDraw(Entity<SpriteComponent> ent)
    {
        if (ent.Comp.Deleted || !ent.Comp.Visible || ent.Comp.ContainerOccluded || ent.Comp.Color.A <= 0f ||
            ent.Owner == _vision.ThermalViewer || _container.IsEntityInContainer(ent))
            return false;

        if (_entityManager.TryGetComponent<MetaDataComponent>(ent, out var meta) &&
            (meta.Flags & MetaDataFlags.Detached) != 0)
            return false;

        // Маскировка прячет картинку от глаз, но тело продолжает излучать тепло, поэтому Stealth пропускаем.
        var outlineOnly = false;
        foreach (var postShader in _sprite.GetPostShaders(ent.Comp))
        {
            if (postShader.Id == ContentPostShaderIds.Stealth)
                continue;

            // Не обходим прочие маскирующие постшейдеры. Обычная обводка не скрывает исходный спрайт.
            if (outlineOnly || postShader.Id != ContentPostShaderIds.InteractionOutline)
                return false;

            outlineOnly = true;
        }

        return !outlineOnly ||
               _entityManager.TryGetComponent<InteractionOutlineComponent>(ent, out var outline) && outline.Active;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewport = args.Viewport;
        var eye = viewport.Eye!;
        var worldToScreen = viewport.GetWorldToLocalMatrix();
        if (!Matrix3x2.Invert(worldToScreen, out var screenToWorld))
            return;

        var origin = _transform.GetMapCoordinates(_vision.ThermalViewer!.Value);
        if (origin.MapId != args.MapId)
            return;

        _contacts.Clear();
        var requiredSize = Vector2i.Zero;
        var rangeSquared = _vision.ThermalRange * _vision.ThermalRange;
        var query = _entityManager.EntityQueryEnumerator<FishThermalSignatureComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var heat, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId || !CanDraw((uid, sprite)))
                continue;
            var (position, rotation) = _transform.GetWorldPositionRotation(xform);
            if (Vector2.DistanceSquared(position, origin.Position) > rangeSquared)
                continue;
            var color = FishThermalPalette.GetColor(heat.Temperature);
            if (color.A <= 0f)
                continue;

            var worldBounds = _sprite.CalculateBounds((uid, sprite), position, rotation, eye.Rotation);
            if (!worldBounds.CalcBoundingBox().Intersects(args.WorldAABB))
                continue;
            var bounds = worldToScreen.TransformBox(worldBounds);
            var start = new Vector2(MathF.Floor(bounds.Left) - 6, MathF.Floor(bounds.Bottom) - 6);
            var size = new Vector2i((int) MathF.Ceiling(bounds.Width) + 12, (int) MathF.Ceiling(bounds.Height) + 12);
            if (size.X <= 0 || size.Y <= 0 || size.X > 2048 || size.Y > 2048)
                continue;

            var screenPosition = viewport.WorldToLocal(position);
            _contacts.Add(new Contact((uid, sprite), screenPosition, start, bounds.Top, rotation + eye.Rotation, color));
            requiredSize = Vector2i.ComponentMax(requiredSize, size);
        }

        if (_contacts.Count == 0)
            return;

        _contacts.Sort(static (left, right) =>
        {
            var depth = left.Sprite.Comp.DrawDepth.CompareTo(right.Sprite.Comp.DrawDepth);
            if (depth != 0)
                return depth;
            var order = left.Sprite.Comp.RenderOrder.CompareTo(right.Sprite.Comp.RenderOrder);
            if (order != 0)
                return order;
            var y = left.Bottom.CompareTo(right.Bottom);
            return y != 0 ? y : left.Sprite.Owner.CompareTo(right.Sprite.Owner);
        });
        var resources = _resources.GetForViewport(viewport, static _ => new Target());
        // Выделяем размер до отрисовки: очередь команд ещё может ссылаться на предыдущую текстуру.
        resources.Ensure(_clyde, requiredSize);
        var target = resources.Texture!;
        var screen = args.RenderHandle.DrawingHandleScreen;
        var handle = args.WorldHandle;
        var oldShader = handle.GetShader();
        var oldTransform = handle.GetTransform();
        try
        {
            _shader.SetParameter("activation", Math.Clamp(
                (float) (_timing.RealTime - _vision.ThermalActivatedAt).TotalSeconds / 0.18f, 0f, 1f));
            _shader.SetParameter("glow_width", 1f);
            foreach (var contact in _contacts)
            {
                var ent = contact.Sprite;
                // DrawEntity сохраняет направления RSI, слои и анимации без подмены компонентов.
                screen.RenderInRenderTarget(target, () =>
                {
                    screen.SetTransform(Matrix3x2.Identity);
                    screen.UseShader(null);
                    screen.DrawEntity(ent, contact.Position - contact.Start,
                        viewport.RenderScale / eye.Zoom, contact.Rotation,
                        overrideDirection: ent.Comp.EnableDirectionOverride ? ent.Comp.DirectionOverride : null,
                        sprite: ent.Comp);
                }, Color.Transparent);
                screen.SetTransform(screenToWorld);
                screen.UseShader(_shader);
                screen.DrawTextureRect(target.Texture, UIBox2.FromDimensions(contact.Start, target.Size), contact.Color);
            }
        }
        finally
        {
            handle.UseShader(oldShader);
            handle.SetTransform(oldTransform);
        }
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();
        _shader.Dispose();
        base.DisposeBehavior();
    }

    private sealed class Target : IDisposable
    {
        public IRenderTexture? Texture;

        public void Ensure(IClyde clyde, Vector2i size)
        {
            if (Texture != null && Texture.Size.X >= size.X && Texture.Size.Y >= size.Y)
                return;
            size = Vector2i.ComponentMax(size, Texture?.Size ?? Vector2i.Zero);
            size = new Vector2i((size.X + 31) / 32 * 32, (size.Y + 31) / 32 * 32);
            Texture?.Dispose();
            Texture = clyde.CreateRenderTarget(size, new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                name: "fish-thermal-sprite");
        }

        public void Dispose() => Texture?.Dispose();
    }

    private readonly record struct Contact(Entity<SpriteComponent> Sprite, Vector2 Position, Vector2 Start,
        float Bottom, Angle Rotation, Color Color);
}
