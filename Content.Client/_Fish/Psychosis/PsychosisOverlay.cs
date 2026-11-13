using System.Numerics;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Fish.Psychosis;

public sealed partial class PsychosisOverlay : Overlay
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const float StrongThreshold = 0.5f;

    private static readonly ProtoId<ShaderPrototype> StrongShader = "FishPsychosis";
    private static readonly ProtoId<ShaderPrototype> MildShader = "FishPsychosisMild";

    private float _intensity; // 0..1
    private ShaderInstance? _strongShaderInstance;
    private ShaderInstance? _mildShaderInstance;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;
    public override bool RequestScreenTexture => true;

    public void SetIntensity(float value)
    {
        _intensity = Math.Clamp(value, 0f, 1f);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _intensity > 0.01f && _cfg.GetCVar(FishCVars.EnablePsychosisEffects);
    }

    /// <summary>
    /// Применяет слабый шейдер резкости до 50 психоза и усиленный шейдер выше этого порога.
    /// </summary>
    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.ScreenHandle;
        var screenSize = args.Viewport.Size;
        var previousShader = handle.GetShader();

        if (_intensity < StrongThreshold)
        {
            _mildShaderInstance ??= _prototypeManager.Index(MildShader).InstanceUnique();
            _mildShaderInstance.SetParameter("SCREEN_TEXTURE", ScreenTexture);
            _mildShaderInstance.SetParameter("intensity", _intensity / StrongThreshold);
            _mildShaderInstance.SetParameter("screen_size", screenSize);

            handle.UseShader(_mildShaderInstance);
            handle.DrawRect(new UIBox2(Vector2.Zero, screenSize), Color.White);
            handle.UseShader(previousShader);
            return;
        }

        // Параболический рост силы эффекта на отрезке 50–100 психоза.
        var progress = Math.Clamp((_intensity - StrongThreshold) / (1f - StrongThreshold), 0f, 1f);
        var strength = progress * progress;

        _strongShaderInstance ??= _prototypeManager.Index(StrongShader).InstanceUnique();
        _strongShaderInstance.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _strongShaderInstance.SetParameter("intensity", strength);
        _strongShaderInstance.SetParameter("time", (float) _timing.CurTime.TotalSeconds);
        _strongShaderInstance.SetParameter("screen_size", screenSize);

        handle.UseShader(_strongShaderInstance);
        handle.DrawRect(new UIBox2(Vector2.Zero, screenSize), Color.White);
        handle.UseShader(previousShader);
    }
}
