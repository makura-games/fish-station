using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Configuration;
using Content.Shared.CCVar;

namespace Content.Client._Fish.Graphics.Vision;

/// <summary>Applies the cold world palette before world indicators and the final FOV mask.</summary>
internal sealed partial class FishThermalWorldOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "FishThermalWorld";
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IGameTiming _timing = default!;
    private readonly FishVisionSystem _vision;
    private readonly ShaderInstance _shader;
    private readonly FishThermalExposure _exposure;
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    public override bool RequestScreenTexture => true;

    public FishThermalWorldOverlay(FishVisionSystem vision)
    {
        IoCManager.InjectDependencies(this);
        _vision = vision;
        _exposure = new(vision);
        _shader = _prototype.Index(Shader).InstanceUnique();
        ZIndex = -99; // Плашки действий и значки рисуются поверх холодной палитры.
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args) =>
        _vision.ThermalViewer != null && args.Viewport.Eye != null && args.Viewport.Eye == _eye.CurrentEye;

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;
        var handle = args.WorldHandle;
        var shader = handle.GetShader();
        var transform = handle.GetTransform();
        try
        {
            _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
            _shader.SetParameter("EXPOSURE_TEXTURE", _exposure.Draw(in args, ScreenTexture, _vision.ThermalActivatedAt));
            _shader.SetParameter("activation", _configuration.GetCVar(CCVars.ReducedMotion) ? 1f :
                Math.Clamp((float) (_timing.RealTime - _vision.ThermalActivatedAt).TotalSeconds / 0.18f, 0f, 1f));
            handle.UseShader(_shader);
            handle.SetTransform(Matrix3x2.Identity);
            handle.DrawRect(args.WorldBounds, Color.White);
        }
        finally
        {
            handle.UseShader(shader);
            handle.SetTransform(transform);
        }
    }

    internal void ResetExposure() => _exposure.Reset();

    protected override void DisposeBehavior()
    {
        _exposure.Dispose();
        _shader.Dispose();
        base.DisposeBehavior();
    }
}
