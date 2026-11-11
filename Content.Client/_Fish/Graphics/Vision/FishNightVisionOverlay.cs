using System.Numerics;
using Content.Client.Graphics;
using Content.Shared.CCVar;
using Content.Shared._Fish.Graphics;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Fish.Graphics.Vision;

/// <summary>
/// Applies device optics before world indicators and the engine's final FOV mask.
/// </summary>
internal sealed partial class FishNightVisionOverlay : Overlay
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IClyde _clyde = default!;

    private static readonly ProtoId<ShaderPrototype> Shader = "FishNightVision";
    private readonly FishVisionSystem _vision;
    private readonly FishNightVisionPresentation _presentation;
    private readonly ShaderInstance _shader;
    private readonly ShaderInstance _exposure;
    private readonly OverlayResourceCache<ExposureResources> _resources = new();
    private static readonly ProtoId<ShaderPrototype> ExposureShader = "FishNightVisionExposure";

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    public override bool RequestScreenTexture => true;

    public FishNightVisionOverlay(FishVisionSystem vision, FishNightVisionPresentation presentation)
    {
        IoCManager.InjectDependencies(this);
        _vision = vision;
        _presentation = presentation;
        _shader = _prototype.Index(Shader).InstanceUnique();
        _exposure = _prototype.Index(ExposureShader).InstanceUnique();
        ZIndex = -100; // До DoAfter и статусных значков, чтобы не перекрашивать интерфейс.
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args) =>
        _presentation.Source != null && args.Viewport.Eye != null && args.Viewport.Eye == _eye.CurrentEye;

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        var previousShader = handle.GetShader();
        var previousTransform = handle.GetTransform();
        try
        {
            var size = (Vector2) args.Viewport.Size;

            var exposure = DrawExposure(in args);
            _vision.SetVisibilityMask(_shader, in args);
            _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
            _shader.SetParameter("EXPOSURE_TEXTURE", exposure);
            _shader.SetParameter("viewport_size", size);
            _shader.SetParameter("phosphor_color", _presentation.Color);
            _shader.SetParameter("aperture_radii", FishNightVisionPresentation.GetApertureRadii(size));
            _shader.SetParameter("edge_fraction", FishNightVisionPresentation.EdgeFraction);
            _shader.SetParameter("activation", _presentation.GetActivation(_timing.RealTime));
            _shader.SetParameter("noise_enabled", _configuration.GetCVar(FishVisionCVars.NightVisionNoise) && !_configuration.GetCVar(CCVars.ReducedMotion));
            _shader.SetParameter("noise_frame", (float) (Math.Floor(_timing.RealTime.TotalSeconds * 24) % 4096));
            handle.SetTransform(Matrix3x2.Identity);
            handle.UseShader(_shader);
            handle.DrawRect(args.WorldBounds, Color.White);
        }
        finally
        {
            handle.UseShader(previousShader);
            handle.SetTransform(previousTransform);
        }
    }

    protected override void DisposeBehavior()
    {
        ResetExposure();
        _exposure.Dispose();
        _shader.Dispose();
        base.DisposeBehavior();
    }

    internal void ResetExposure() => _resources.Dispose();

    private Texture DrawExposure(in OverlayDrawArgs args)
    {
        var cache = _resources.GetForViewport(args.Viewport, _ => new ExposureResources(_clyde));
        var now = _timing.RealTime;
        var reset = !cache.Initialized || cache.Revision != _presentation.Revision;
        _vision.SetVisibilityMask(_exposure, in args);
        _exposure.SetParameter("SCREEN_TEXTURE", ScreenTexture!);
        _exposure.SetParameter("PREVIOUS_TEXTURE", cache.Previous.Texture);
        _exposure.SetParameter("reset_history", reset);
        _exposure.SetParameter("adaptation", FishNightVisionPresentation.GetAdaptationFactors(
            (float) (now - cache.LastDraw).TotalSeconds));
        _exposure.SetParameter("viewport_size", (Vector2) args.Viewport.Size);
        _exposure.SetParameter("aperture_radii", FishNightVisionPresentation.GetApertureRadii(args.Viewport.Size));

        // Храним только одно число, а не предыдущий кадр: движения не оставляют шлейфов.
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
        cache.Revision = _presentation.Revision;
        cache.Initialized = true;
        return cache.Previous.Texture;
    }

    private sealed class ExposureResources : IDisposable
    {
        public IRenderTexture Previous;
        public IRenderTexture Next;
        public bool Initialized;
        public uint Revision;
        public TimeSpan LastDraw;

        public ExposureResources(IClyde clyde)
        {
            // Float исключает ступеньки и остановку медленной адаптации из-за округления до 8 бит.
            var format = new RenderTargetFormatParameters(RenderTargetColorFormat.R32F);
            Previous = clyde.CreateRenderTarget(Vector2i.One, format, name: "fish-nv-exposure-a");
            Next = clyde.CreateRenderTarget(Vector2i.One, format, name: "fish-nv-exposure-b");
        }

        public void Dispose()
        {
            Previous.Dispose();
            Next.Dispose();
        }
    }
}
