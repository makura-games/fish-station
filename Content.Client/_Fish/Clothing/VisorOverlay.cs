using Content.Shared._Fish.Clothing;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Fish.Clothing;

/// <summary>
/// Draws the helmet visor mask in world space without obscuring the UI.
/// </summary>
public sealed partial class VisorOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "FishVisorMask";
    private const float OpacityEpsilon = 0.001f;

    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly ShaderInstance _shader;
    private float _targetOpacity;
    private float _fadeDuration = 0.18f;
    private float _openingWidth = 0.84f;
    private float _openingHeight = 0.62f;
    private float _edgeSoftness = 0.055f;
    private float _cornerRadius = 0.085f;
    private float _darkness = 0.72f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => CurrentOpacity > OpacityEpsilon;

    public float CurrentOpacity { get; private set; }
    public bool IsFullyTransparent => CurrentOpacity <= OpacityEpsilon;

    public VisorOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototype.Index(Shader).InstanceUnique();
    }

    public void Configure(VisorOverlayComponent component)
    {
        _openingWidth = Math.Clamp(component.OpeningWidth, 0.2f, 1f);
        _openingHeight = Math.Clamp(component.OpeningHeight, 0.2f, 1f);
        _edgeSoftness = Math.Clamp(component.EdgeSoftness, 0.001f, 0.25f);
        _cornerRadius = Math.Clamp(component.CornerRadius, 0f, 0.25f);
        _darkness = Math.Clamp(component.Darkness, 0f, 1f);
        _fadeDuration = Math.Max(component.FadeDuration, 0f);
    }

    public void SetEnabled(bool enabled, bool immediate = false)
    {
        _targetOpacity = enabled ? 1f : 0f;

        if (immediate)
            CurrentOpacity = _targetOpacity;
    }

    public void AdvanceFade(float frameTime)
    {
        if (MathHelper.CloseTo(CurrentOpacity, _targetOpacity) || _fadeDuration <= 0f)
        {
            CurrentOpacity = _targetOpacity;
            return;
        }

        var step = frameTime / _fadeDuration;
        CurrentOpacity = CurrentOpacity < _targetOpacity
            ? Math.Min(CurrentOpacity + step, _targetOpacity)
            : Math.Max(CurrentOpacity - step, _targetOpacity);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        AdvanceFade(args.DeltaSeconds);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return CurrentOpacity > OpacityEpsilon &&
               args.Viewport.Eye == _eye.CurrentEye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("OverlayOpacity", CurrentOpacity);
        _shader.SetParameter("OpeningWidth", _openingWidth);
        _shader.SetParameter("OpeningHeight", _openingHeight);
        _shader.SetParameter("EdgeSoftness", _edgeSoftness);
        _shader.SetParameter("CornerRadius", _cornerRadius);
        _shader.SetParameter("Darkness", _darkness);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
