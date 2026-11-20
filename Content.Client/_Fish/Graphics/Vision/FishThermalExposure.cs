using System.Numerics;
using Content.Client.Graphics;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Fish.Graphics.Vision;

/// <summary>GPU-only exposure history; stores a single gain, never a delayed world image.</summary>
internal sealed class FishThermalExposure : IDisposable
{
    private static readonly ProtoId<ShaderPrototype> ExposureShader = "FishThermalExposure";
    private readonly IClyde _clyde = IoCManager.Resolve<IClyde>();
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly IConfigurationManager _configuration = IoCManager.Resolve<IConfigurationManager>();
    private readonly ShaderInstance _shader = IoCManager.Resolve<IPrototypeManager>()
        .Index(ExposureShader).InstanceUnique();
    private readonly OverlayResourceCache<History> _history = new();
    private readonly FishVisionSystem _vision;

    public FishThermalExposure(FishVisionSystem vision)
    {
        _vision = vision;
    }

    internal static Vector2 Adaptation(float elapsed, bool reducedMotion) => reducedMotion ? Vector2.One : new(
        1f - MathF.Exp(-Math.Max(0f, elapsed) / 0.25f),
        1f - MathF.Exp(-Math.Max(0f, elapsed) / 0.8f));

    public Texture Draw(in OverlayDrawArgs args, Texture source, TimeSpan activation)
    {
        var history = _history.GetForViewport(args.Viewport, _ => new History(_clyde));
        var now = _timing.RealTime;
        _vision.SetVisibilityMask(_shader, in args);
        _shader.SetParameter("SCREEN_TEXTURE", source);
        _shader.SetParameter("PREVIOUS_TEXTURE", history.Previous.Texture);
        _shader.SetParameter("reset_history", !history.Initialized || history.Activation != activation);
        _shader.SetParameter("adaptation", Adaptation((float) (now - history.LastDraw).TotalSeconds,
            _configuration.GetCVar(CCVars.ReducedMotion)));

        var screen = args.RenderHandle.DrawingHandleScreen;
        var shader = screen.GetShader();
        var transform = screen.GetTransform();
        try
        {
            screen.RenderInRenderTarget(history.Next, () =>
            {
                screen.SetTransform(Matrix3x2.Identity);
                screen.UseShader(_shader);
                screen.DrawRect(UIBox2.FromDimensions(Vector2.Zero, Vector2.One), Color.White);
            }, Color.Black);
        }
        finally
        {
            screen.UseShader(shader);
            screen.SetTransform(transform);
        }
        (history.Previous, history.Next) = (history.Next, history.Previous);
        history.LastDraw = now;
        history.Activation = activation;
        history.Initialized = true;
        return history.Previous.Texture;
    }

    public void Dispose()
    {
        _history.Dispose();
        _shader.Dispose();
    }

    internal void Reset() => _history.Dispose();

    private sealed class History : IDisposable
    {
        public IRenderTexture Previous;
        public IRenderTexture Next;
        public TimeSpan Activation;
        public TimeSpan LastDraw;
        public bool Initialized;

        public History(IClyde clyde)
        {
            // R32F сохраняет плавность медленной адаптации без округления до восьми бит.
            var format = new RenderTargetFormatParameters(RenderTargetColorFormat.R32F);
            Previous = clyde.CreateRenderTarget(Vector2i.One, format, name: "fish-thermal-exposure-a");
            Next = clyde.CreateRenderTarget(Vector2i.One, format, name: "fish-thermal-exposure-b");
        }

        public void Dispose()
        {
            Previous.Dispose();
            Next.Dispose();
        }
    }
}
