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

    private float _intensity; // 0..1
    private ShaderInstance? _shader;
    private static readonly ProtoId<ShaderPrototype> Shader = "FishPsychosis";

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public void SetIntensity(float value)
    {
        _intensity = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Применяет шейдер психоза к области экрана при активном эффекте.
    /// </summary>
    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_intensity <= 0.01f || !_cfg.GetCVar(FishCVars.EnablePsychosisEffects))
            return;

        _shader ??= _prototypeManager.Index(Shader).InstanceUnique();
        var handle = args.ScreenHandle;
        var screenSize = args.Viewport.Size;

        _shader.SetParameter("intensity", _intensity);
        _shader.SetParameter("time", (float) _timing.CurTime.TotalSeconds);
        _shader.SetParameter("screen_size", screenSize);
        var previousShader = handle.GetShader();
        handle.UseShader(_shader);
        handle.DrawRect(new UIBox2(Vector2.Zero, screenSize), Color.White);
        handle.UseShader(previousShader);
    }
}