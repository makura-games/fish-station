using System.Numerics;
using System.Runtime.InteropServices;
using Content.Shared.Anomaly.Effects.Components;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Fish.Anomaly;

/// <summary>Показывает направление поля скользящими пылинками на уровне пола.</summary>
internal sealed partial class GravityAnomalyDustOverlay : Overlay
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly SharedTransformSystem _transform;
    private readonly List<WorldRect> _dust = new(256);
    private readonly List<WorldRect> _glints = new(64);
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";
    private readonly ShaderInstance _unshaded;
    private static readonly Color DustColor = Color.FromHex("#d8b6a5");
    private static readonly Color GlintColor = Color.FromHex("#f4ddd0");
    private const float Pixel = 1f / EyeManager.PixelsPerMeter;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    public GravityAnomalyDustOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entities.System<SharedTransformSystem>();
        _unshaded = _prototypes.Index(UnshadedShader).Instance();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args) => _config.GetCVar(FishCVars.ActionParticlesEnabled);

    protected override void Draw(in OverlayDrawArgs args)
    {
        var time = _config.GetCVar(CCVars.ReducedMotion) ? 0.8 : _timing.CurTime.TotalSeconds;
        var anomalies = _entities.EntityQueryEnumerator<GravityAnomalyComponent, TransformComponent>();
        _dust.Clear();
        _glints.Clear();
        while (anomalies.MoveNext(out var uid, out var anomaly, out var xform))
        {
            var field = anomaly.Field;
            if (xform.MapID != args.MapId || field.OuterRadius <= field.InnerRadius ||
                (field.Radial == 0f && field.Tangential == 0f))
                continue;

            var center = _transform.GetWorldPosition(xform);
            if (Vector2.DistanceSquared(args.WorldAABB.ClosestPoint(center), center) > field.OuterRadius * field.OuterRadius)
                continue;

            var count = (int) Math.Clamp(field.OuterRadius * 9, 8, 90);
            for (var i = 0; i < count; i++)
                AppendDust(field, center, uid.Id, i, time);
        }

        if (_dust.Count == 0 && _glints.Count == 0)
            return;

        var handle = args.WorldHandle;
        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);
        handle.DrawRectsUnmodulated(CollectionsMarshal.AsSpan(_dust));
        handle.UseShader(_unshaded);
        handle.DrawRectsUnmodulated(CollectionsMarshal.AsSpan(_glints));
        handle.UseShader(null);
    }

    private void AppendDust(GravityAnomalyField field, Vector2 center, int id, int index, double time)
    {
        // Разные фазы и времена жизни оставляют в поле промежутки, даже когда сила постоянна.
        var seed = unchecked((uint) id * 2654435761u + (uint) index);
        var nearCore = index % 3 == 0;
        var life = (nearCore ? 0.8f : 1.3f) + Noise(seed);
        var cycles = time / life + Noise(seed + 1);
        var cycle = Math.Floor(cycles);
        var phase = (float) (cycles - cycle);
        seed = unchecked(seed + (uint) cycle * 2246822519u);
        var inner = Math.Max(field.InnerRadius, 0.16f);
        var outer = nearCore ? MathHelper.Lerp(inner, field.OuterRadius, 0.55f) : field.OuterRadius;
        var startRadiusSquared = MathHelper.Lerp(inner * inner, outer * outer, Noise(seed + 2));
        var startAngle = MathF.Tau * Noise(seed + 3);
        var offset = Streamline(field, startRadiusSquared, startAngle, phase * life);
        var distance = offset.Length();
        if (distance <= inner || distance > field.OuterRadius)
            return;

        var alpha = Math.Clamp(Math.Min(phase, 1 - phase) * 5f, 0, 1);
        var direction = -field.Radial * offset + field.Tangential * new Vector2(-offset.Y, offset.X);
        var trail = Vector2.Normalize(direction) * (1.5f * Pixel);
        var position = center + offset;
        var glint = index % 4 == 0 || (nearCore && index % 2 == 0);
        var size = (glint ? (index % 8 == 0 ? 3 : 2) : (index % 3 == 0 ? 2 : 1)) * Pixel;
        // Свет ловят лишь отдельные крупинки. Их короткий хвост показывает направление и на тёмном полу.
        var batch = glint ? _glints : _dust;
        var color = glint ? GlintColor : DustColor;
        AddSpeck(batch, position - trail * 3, Pixel, color.WithAlpha(alpha * 0.15f));
        AddSpeck(batch, position - trail * 2, Pixel, color.WithAlpha(alpha * 0.29f));
        AddSpeck(batch, position - trail, Math.Min(2 * Pixel, size), color.WithAlpha(alpha * 0.44f));
        AddSpeck(batch, position, size, color.WithAlpha(alpha * (glint ? 0.72f : 0.87f)));
    }

    /// <summary>Интеграл линии тока: радиальная скорость пропорциональна 1/r, угловая — 1/r².</summary>
    internal static Vector2 Streamline(GravityAnomalyField field, float startRadiusSquared, float startAngle, float age)
    {
        // Коэффициент задаёт скорость перемещения вдоль линии тока.
        var t = 0.72f * age;
        var radiusSquared = startRadiusSquared - 2 * field.Radial * t;
        if (radiusSquared <= 0)
            return Vector2.Zero;

        var turn = MathF.Abs(2 * field.Radial * t) < 0.001f * startRadiusSquared
            ? field.Tangential * t / startRadiusSquared
            : field.Tangential * MathF.Log(startRadiusSquared / radiusSquared) / (2 * field.Radial);
        var angle = startAngle + turn;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * MathF.Sqrt(radiusSquared);
    }

    private static void AddSpeck(List<WorldRect> batch, Vector2 position, float size, Color color)
    {
        var corner = Vector2.Round((position - new Vector2(size * 0.5f)) / Pixel) * Pixel;
        batch.Add(new WorldRect(new Box2(corner, corner + new Vector2(size)), color));
    }

    private static float Noise(uint seed)
    {
        seed ^= seed >> 16;
        seed = unchecked(seed * 0x7feb352du);
        seed ^= seed >> 15;
        seed = unchecked(seed * 0x846ca68bu);
        return ((seed ^ (seed >> 16)) >> 8) / 16777216f;
    }
}
