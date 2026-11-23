using Content.Client._Fish.Netrunner;
using Content.Shared._Fish.Cyberpsychosis;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Fish.Cyberpsychosis;

/// <summary>Renders the experimental cyberpsychosis fullscreen shader below HUD overlays when the local player reaches 10%+.</summary>
public sealed partial class CyberpsychosisOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> ShaderId = "FishCyberpsychosis";

    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private readonly ShaderInstance _shader;
    private float _currentLevel;
    private float _preserveVisor;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    public override bool RequestScreenTexture => true;

    public CyberpsychosisOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _proto.Index(ShaderId).InstanceUnique();
        ZIndex = -1;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye != _eye.CurrentEye)
            return false;

        if (!_entities.TryGetComponent(_player.LocalEntity, out CyberpsychosisComponent? comp) || comp.Level < 10f)
            return false;

        _currentLevel = Math.Clamp(comp.Level / 100f, 0f, 1f);
        _preserveVisor = _entities.System<ShowNetrunnerMindShieldIconsSystem>().IsActive ? 1f : 0f;
        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("Level", _currentLevel);
        _shader.SetParameter("PreserveVisor", _preserveVisor);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }

    protected override void DisposeBehavior()
    {
        _shader.Dispose();
        base.DisposeBehavior();
    }
}
