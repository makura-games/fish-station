using System.Numerics;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;

namespace Content.Client._Fish.Stress;

public sealed partial class StressOverlay : Overlay
{
    [Dependency] private IConfigurationManager _cfg = default!;

    private float _intensity; // 0..1

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public void SetIntensity(float value)
    {
        _intensity = Math.Clamp(value, 0f, 1f);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_intensity <= 0.01f || !_cfg.GetCVar(FishCVars.EnableStressEffects))
            return;

        var handle = args.ScreenHandle;
        var screenSize = args.Viewport.Size;

        float vignetteStrength = _intensity * 0.2f;
        handle.DrawRect(new UIBox2(Vector2.Zero, screenSize), new Color(0, 0, 0, vignetteStrength));
    }
}