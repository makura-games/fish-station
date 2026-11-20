using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._Fish.Graphics.Vision;

/// <summary>
/// Adds device lighting before the engine applies its visibility mask.
/// </summary>
internal sealed partial class FishVisionLightOverlay : Overlay
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IEyeManager _eye = default!;

    private static readonly Color ThermalLighting = new(1f, 1f, 1f, 0.35f);

    private readonly FishVisionSystem _vision;
    private readonly FishNightVisionPresentation _presentation;

    public override OverlaySpace Space => OverlaySpace.BeforeLighting;

    public FishVisionLightOverlay(FishVisionSystem vision, FishNightVisionPresentation presentation)
    {
        IoCManager.InjectDependencies(this);
        _vision = vision;
        _presentation = presentation;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args) =>
        (_presentation.Source != null || _vision.ThermalViewer != null) &&
        args.Viewport.Eye != null && args.Viewport.Eye == _eye.CurrentEye;

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var shader = handle.GetShader();
        var transform = handle.GetTransform();
        try
        {
            handle.UseShader(null);
            handle.SetTransform(System.Numerics.Matrix3x2.Identity);
            var thermal = _vision.ThermalViewer != null;
            var color = thermal ? ThermalLighting : _presentation.LightingColor;
            color.A *= thermal
                ? Math.Clamp((float) (_timing.RealTime - _vision.ThermalActivatedAt).TotalSeconds / 0.18f, 0f, 1f)
                : _presentation.GetActivation(_timing.RealTime);
            handle.DrawRect(args.WorldBounds, color);
        }
        finally
        {
            handle.UseShader(shader);
            handle.SetTransform(transform);
        }
    }
}
