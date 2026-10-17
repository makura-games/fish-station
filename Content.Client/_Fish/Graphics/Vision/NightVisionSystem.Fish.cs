using Content.Shared._Sunrise.NightVision.Components;

namespace Content.Client._Sunrise.Overlays;

public sealed partial class NightVisionSystem
{
    // Согласует старое природное зрение с эффектом надетого ПНВ.
    private bool _fishVisionOverride;

    /// <summary>Suspends legacy device rendering and restores natural vision afterwards.</summary>
    public void SetFishVisionOverride(bool active)
    {
        if (_fishVisionOverride == active)
            return;

        _fishVisionOverride = active;
        if (active)
        {
            if (_effect != null)
                AttemptRemoveVision(_effect.Value, true);
        }
        else if (_player.LocalEntity is { } viewer && TryComp<NightVisionComponent>(viewer, out var vision))
        {
            AttemptAddVision(viewer, vision);
        }
    }
}
