using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._Fish.UserInterface.Crt;

internal sealed class FishCrtEffectRenderer
{
    private static readonly ProtoId<ShaderPrototype> Shader = "FishCrtUiEffects";

    private readonly ShaderInstance _shader;

    public FishCrtEffectRenderer()
    {
        _shader = IoCManager.Resolve<IPrototypeManager>().Index(Shader).InstanceUnique();
    }

    public void Draw(
        DrawingHandleScreen handle,
        float width,
        float height,
        float uiScale,
        FishCrtEffects effects,
        float scanlineSpacing,
        float scanlineThickness,
        float rgbWidth,
        float stripeWidth,
        float scanlineOpacity,
        float rgbOpacity,
        Color stripeColor)
    {
        if (effects == FishCrtEffects.None || width <= 0 || height <= 0)
            return;

        _shader.SetParameter("size", new Vector2(width, height));
        _shader.SetParameter(
            "horizontalScanlines",
            (effects & FishCrtEffects.HorizontalScanlines) != 0);
        _shader.SetParameter(
            "rgbSubpixels",
            (effects & FishCrtEffects.RgbSubpixels) != 0);
        _shader.SetParameter(
            "diagonalStripes",
            (effects & FishCrtEffects.DiagonalStripes) != 0);
        _shader.SetParameter("scanlineSpacing", Math.Max(2f, scanlineSpacing * uiScale));
        _shader.SetParameter("scanlineThickness", Math.Max(1f, scanlineThickness * uiScale));
        _shader.SetParameter("rgbWidth", Math.Max(1f, rgbWidth * uiScale));
        _shader.SetParameter("stripeWidth", Math.Max(2f, stripeWidth * uiScale));
        _shader.SetParameter("scanlineOpacity", Math.Clamp(scanlineOpacity, 0, 1));
        _shader.SetParameter("rgbOpacity", Math.Clamp(rgbOpacity, 0, 1));
        _shader.SetParameter("stripeColor", stripeColor);

        var previousShader = handle.GetShader();
        handle.UseShader(_shader);
        handle.DrawRect(UIBox2.FromDimensions(Vector2.Zero, new Vector2(width, height)), Color.White);
        handle.UseShader(previousShader);
    }
}
