using System.Numerics;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Configuration;
using Robust.Shared.Random;

namespace Content.Client._Fish.Neuro;

public sealed partial class NeuroOverlay : Overlay
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private float _intensity; // 0..1

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public void SetIntensity(float value)
    {
        _intensity = Math.Clamp(value, 0f, 1f);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_intensity <= 0.01f || !_cfg.GetCVar(FishCVars.EnableNeuroEffects))
            return;

        var handle = args.ScreenHandle;
        var screenSize = args.Viewport.Size;

        if (_intensity > 0.2f)
        {
            var colorShift = _intensity * 0.3f;
            handle.DrawRect(
                new UIBox2(Vector2.Zero, screenSize),
                new Color(colorShift, 0f, colorShift * 0.3f, colorShift * 0.3f));
        }

        if (_intensity > 0.4f)
        {
            var blockCount = (int) (_intensity * 50);
            for (var i = 0; i < blockCount; i++)
            {
                var position = new Vector2(
                    _random.NextFloat() * screenSize.X,
                    _random.NextFloat() * screenSize.Y);
                var size = _random.NextFloat(2f, 8f) * _intensity;
                handle.DrawRect(
                    new UIBox2(position, position + new Vector2(size, size)),
                    new Color(1f, 1f, 1f, _intensity * 0.2f));
            }
        }

        if (_intensity > 0.7f)
            handle.DrawRect(
                new UIBox2(Vector2.Zero, screenSize),
                new Color(0f, 0f, 0f, (_intensity - 0.7f) * 0.3f));
    }
}
