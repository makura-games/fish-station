using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using System.Numerics;

namespace Content.Client._Fish.Stress;

public sealed class StressHudOverlay : Overlay
{
    private float? _stress;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public void SetStress(float? value)
    {
        _stress = value;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_stress == null)
            return;

        const float width = 180f;
        const float height = 8f;
        const float margin = 20f;
        var viewport = args.Viewport.Size;
        var min = new Vector2(margin, viewport.Y - height - margin);
        var max = min + new Vector2(width, height);
        var handle = args.ScreenHandle;

        handle.DrawRect(new UIBox2(min, max), new Color(0, 0, 0, 0.65f));
        var fillMax = new Vector2(min.X + width * Math.Clamp(_stress.Value / 100f, 0f, 1f), max.Y);
        handle.DrawRect(
            new UIBox2(min, fillMax),
            Color.InterpolateBetween(Color.Green, Color.Red, Math.Clamp(_stress.Value / 100f, 0f, 1f)));
    }
}