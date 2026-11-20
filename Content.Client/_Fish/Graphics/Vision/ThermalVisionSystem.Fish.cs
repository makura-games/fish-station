using Content.Shared._Sunrise.ThermalVision;

namespace Content.Client._Sunrise.ThermalVision;

public sealed partial class ThermalVisionSystem
{
    // Приостанавливает старый эффект на время работы термальных очков.
    private bool _fishVisionOverride;

    /// <summary>Suspends the legacy effect while Fish goggles provide thermal vision.</summary>
    public void SetFishVisionOverride(bool active)
    {
        if (_fishVisionOverride == active)
            return;

        _fishVisionOverride = active;
        if (active)
            RemoveNightVision();
        else if (_player.LocalEntity is { } viewer && HasComp<ThermalVisionComponent>(viewer))
            AddNightVision(viewer);
    }
}
