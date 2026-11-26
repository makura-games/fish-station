using Robust.Client.Graphics;

namespace Content.Client._Fish.Cyberpsychosis;

/// <summary>Registers the experimental cyberpsychosis fullscreen overlay on the client.</summary>
public sealed partial class CyberpsychosisOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new CyberpsychosisOverlay());
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<CyberpsychosisOverlay>();
        base.Shutdown();
    }
}
