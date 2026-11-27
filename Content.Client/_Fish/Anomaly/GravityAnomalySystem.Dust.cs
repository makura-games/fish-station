using Content.Client._Fish.Anomaly;
using Robust.Client.Graphics;

namespace Content.Client.Anomaly.Effects;

public sealed partial class GravityAnomalySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new GravityAnomalyDustOverlay());
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<GravityAnomalyDustOverlay>();
        base.Shutdown();
    }
}
