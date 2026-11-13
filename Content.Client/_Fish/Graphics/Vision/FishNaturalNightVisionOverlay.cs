using System.Numerics;
using Content.Client.Graphics;
using Content.Shared.CCVar;
using Content.Shared._Fish.Vision;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using VisionSystem = Content.Client.NightVision.NightVisionSystem;

namespace Content.Client._Fish.Graphics.Vision;

/// <summary>Adapts innate vision to visible lighting without coloring world indicators.</summary>
internal sealed partial class FishNaturalNightVisionOverlay : Overlay
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IConfigurationManager _configuration = default!;

    private static readonly ProtoId<ShaderPrototype> Shader = "FishNaturalNightVision";
    private static readonly ProtoId<ShaderPrototype> ExposureShader = "FishNaturalNightVisionExposure";
    private static readonly ProtoId<ShaderPrototype> LightingShader = "FishNaturalNightVisionLighting";
    private readonly VisionSystem _vision;
    private readonly ShaderInstance _shader;
    private readonly ShaderInstance _exposure;
    private readonly ShaderInstance _lighting;
    private readonly OverlayResourceCache<ExposureResources> _resources = new();
    private EntityUid? _viewer;
    private FishNaturalNightVisionComponent? _profile;
    private float _lightingStrength;
    private Vector2 _lightingRadii;

    public override OverlaySpace Space => OverlaySpace.BeforeLighting | OverlaySpace.WorldSpaceBelowFOV;

    public FishNaturalNightVisionOverlay(VisionSystem vision)
    {
        IoCManager.InjectDependencies(this);
        _vision = vision;
        _shader = _prototype.Index(Shader).InstanceUnique();
        _exposure = _prototype.Index(ExposureShader).InstanceUnique();
        _lighting = _prototype.Index(LightingShader).InstanceUnique();
        ZIndex = -1; // После подготовки света (-6/-5), но до DoAfter и статусных значков (0).
    }

    internal void SetSource(EntityUid viewer, FishNaturalNightVisionComponent profile)
    {
        if (_viewer != viewer || _profile != profile)
            Reset();
        _viewer = viewer;
        _profile = profile;
        _lightingStrength = FiniteClamp(profile.LightingStrength, 0f, 0.35f, 0.25f);
        var radius = FiniteClamp(profile.FullLightRadius, 0f, 32f, 2.5f);
        _lightingRadii = new Vector2(radius, radius + FiniteClamp(profile.LightFadeWidth, 0.1f, 32f, 0.5f));
    }

    internal void Reset()
    {
        _viewer = null;
        _profile = null;
        _resources.Dispose();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        RequestScreenTexture = args.Space == OverlaySpace.WorldSpaceBelowFOV;
        return _viewer != null && _profile != null && args.Viewport.Eye != null &&
            args.Viewport.Eye == _eye.CurrentEye && _vision.IsFishNaturalLightingEnabled(in args);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_profile == null)
            return;

        var cache = _resources.GetForViewport(args.Viewport, _ => new ExposureResources(_clyde));
        var lighting = args.Space == OverlaySpace.BeforeLighting;
        if (lighting && !cache.Initialized || !lighting && ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        var previousShader = handle.GetShader();
        var previousTransform = handle.GetTransform();
        try
        {
            var geometry = GetLightingGeometry(in args.WorldBounds, args.Viewport.Eye!.Position.Position);
            if (!lighting)
            {
                DrawExposure(in args, cache, geometry);
                _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture!);
                _shader.SetParameter("VISION_LIGHT_TEXTURE", args.Viewport.LightRenderTarget.Texture);
                _shader.SetParameter("APPLIED_TEXTURE", cache.Next.Texture);
                _shader.SetParameter("darkness_saturation", FiniteClamp(_profile.DarknessSaturation, 0f, 1f, 0.15f));
                _shader.SetParameter("shadow_contrast", FiniteClamp(_profile.ShadowContrast, 1f, 1.5f, 1.12f));
                _shader.SetParameter("grain_strength", FiniteClamp(_profile.GrainStrength, 0f, 0.75f, 0.4f));
                _shader.SetParameter("viewport_size", (Vector2) args.Viewport.Size);
                // Уменьшение движения сохраняет зерно, но останавливает его мерцание.
                _shader.SetParameter("noise_frame", _configuration.GetCVar(CCVars.ReducedMotion)
                    ? 0f
                    : (float) (Math.Floor(_timing.RealTime.TotalSeconds * 12) % 4096));
                _shader.SetParameter("tint", _profile.Tint);
            }

            // Отдельный шейдер подсветки не содержит сэмплер светового буфера, в который мы рисуем.
            var shader = lighting ? _lighting : _shader;
            SetLightingParameters(shader, geometry);
            shader.SetParameter("EXPOSURE_TEXTURE", cache.Previous.Texture);
            shader.SetParameter("lighting_strength", _lightingStrength);
            handle.SetTransform(Matrix3x2.Identity);
            handle.UseShader(shader);
            handle.DrawRect(args.WorldBounds, Color.White);
        }
        finally
        {
            handle.UseShader(previousShader);
            handle.SetTransform(previousTransform);
        }
    }

    /// <summary>Maps the FOV eye to quad UV coordinates and preserves world distances at any zoom.</summary>
    internal static Vector4 GetLightingGeometry(in Box2Rotated bounds, Vector2 eyePosition)
    {
        // Убираем поворот камеры; её смещение не должно сдвигать область зрения относительно глаз.
        var offset = (-bounds.Rotation).RotateVec(eyePosition - bounds.Center);
        var size = bounds.Box.Size;
        return new Vector4(new Vector2(0.5f) + offset / size, size.X, size.Y);
    }

    /// <summary>Exponential response factors remain stable when the frame rate changes.</summary>
    internal static Vector2 GetAdaptationFactors(float elapsed, float lightSeconds, float darkSeconds)
    {
        elapsed = float.IsFinite(elapsed) ? Math.Max(elapsed, 0f) : 0f;
        return new Vector2(
            1f - MathF.Exp(-3f * elapsed / FiniteClamp(lightSeconds, 0.01f, 30f, 0.2f)),
            1f - MathF.Exp(-3f * elapsed / FiniteClamp(darkSeconds, 0.01f, 30f, 1.75f)));
    }

    private static float FiniteClamp(float value, float min, float max, float fallback) =>
        float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    private void SetLightingParameters(ShaderInstance shader, Vector4 geometry)
    {
        shader.SetParameter("lighting_geometry", geometry);
        shader.SetParameter("lighting_radii", _lightingRadii);
    }

    private void DrawExposure(in OverlayDrawArgs args, ExposureResources cache, Vector4 geometry)
    {
        var now = _timing.RealTime;
        SetLightingParameters(_exposure, geometry);
        _exposure.SetParameter("SCREEN_TEXTURE", ScreenTexture!);
        _exposure.SetParameter("VISION_LIGHT_TEXTURE", args.Viewport.LightRenderTarget.Texture);
        _exposure.SetParameter("lighting_mask_enabled", true);
        _exposure.SetParameter("PREVIOUS_TEXTURE", cache.Previous.Texture);
        _exposure.SetParameter("reset_history", !cache.Initialized);
        _exposure.SetParameter("lighting_strength", _lightingStrength);
        _exposure.SetParameter("adaptation", GetAdaptationFactors(cache.Initialized ? (float) (now - cache.LastDraw).TotalSeconds : 0f,
            _profile!.LightAdaptationSeconds, _profile.DarkAdaptationSeconds));

        // Измеряем свет после освещения; BeforeLighting получит результат на следующем кадре.
        var screen = args.RenderHandle.DrawingHandleScreen;
        var shader = screen.GetShader();
        var transform = screen.GetTransform();
        try
        {
            screen.RenderInRenderTarget(cache.Next, () =>
            {
                screen.SetTransform(Matrix3x2.Identity);
                screen.UseShader(_exposure);
                screen.DrawRect(UIBox2.FromDimensions(Vector2.Zero, Vector2.One), Color.White);
            }, Color.Black);
        }
        finally
        {
            screen.UseShader(shader);
            screen.SetTransform(transform);
        }
        (cache.Previous, cache.Next) = (cache.Next, cache.Previous);
        cache.LastDraw = now;
        cache.Initialized = true;
    }

    protected override void DisposeBehavior()
    {
        Reset();
        _shader.Dispose();
        _exposure.Dispose();
        _lighting.Dispose();
        base.DisposeBehavior();
    }

    private sealed class ExposureResources : IDisposable
    {
        public IRenderTexture Previous;
        public IRenderTexture Next;
        public bool Initialized;
        public TimeSpan LastDraw;

        public ExposureResources(IClyde clyde)
        {
            var format = new RenderTargetFormatParameters(RenderTargetColorFormat.R32F);
            Previous = clyde.CreateRenderTarget(Vector2i.One, format, name: "fish-natural-exposure-a");
            Next = clyde.CreateRenderTarget(Vector2i.One, format, name: "fish-natural-exposure-b");
        }

        public void Dispose()
        {
            Previous.Dispose();
            Next.Dispose();
        }
    }
}
