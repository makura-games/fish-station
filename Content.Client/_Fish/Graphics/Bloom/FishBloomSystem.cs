using Content.Shared._Fish.Graphics;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;

namespace Content.Client._Fish.Graphics.Bloom;

/// <summary>
/// Applies saved settings and temporary previews to the emissive bloom overlay.
/// </summary>
public sealed partial class FishBloomSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IOverlayManager _overlay = default!;

    private FishBloomOverlay? _bloom;
    private bool _enabled;
    private float _strength;
    private FishBloomQuality _quality;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_configuration, FishBloomCVars.Strength, PreviewStrength, true);
        Subs.CVar(_configuration, FishBloomCVars.Quality, PreviewQuality, true);
        Subs.CVar(_configuration, FishBloomCVars.Enabled, PreviewEnabled, true);
    }

    public override void Shutdown()
    {
        RemoveOverlay();
        base.Shutdown();
    }

    /// <summary>Enables or disables the preview without modifying the saved setting.</summary>
    public void PreviewEnabled(bool enabled)
    {
        _enabled = enabled;
        UpdateOverlay();
    }

    /// <summary>Applies a finite intensity between zero and one without saving it.</summary>
    public void PreviewStrength(float strength)
    {
        _strength = float.IsNaN(strength) ? 0f : Math.Clamp(strength, 0f, 1f);
        UpdateOverlay();
    }

    /// <summary>Applies a resolution profile without modifying the saved setting.</summary>
    public void PreviewQuality(string quality)
    {
        _quality = FishBloomQualityExtensions.Parse(quality);
        UpdateOverlay();
    }

    private void UpdateOverlay()
    {
        if (!_enabled || _strength <= 0f)
        {
            RemoveOverlay();
            return;
        }

        if (_bloom == null)
        {
            _bloom = new FishBloomOverlay(EntityManager);
            _overlay.AddOverlay(_bloom);
        }

        _bloom.Strength = _strength;
        _bloom.Quality = _quality;
    }

    private void RemoveOverlay()
    {
        if (_bloom == null)
            return;

        _overlay.RemoveOverlay(_bloom);
        _bloom.Dispose();
        _bloom = null;
    }
}
