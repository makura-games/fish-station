using Content.Shared._Fish.Vision;
using Content.Shared._Sunrise.ThermalVision;
using Content.Shared.Eye.Blinding.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using LegacyNightVisionSystem = Content.Client.NightVision.NightVisionSystem;
using LegacyThermalVisionSystem = Content.Client._Sunrise.ThermalVision.ThermalVisionSystem;

namespace Content.Client._Fish.Graphics.Vision;

/// <summary>Owns device optics and restores legacy natural vision when devices are removed.</summary>
public sealed partial class FishVisionSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILightManager _light = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private EntityQuery<MapComponent> _mapQuery = default!;
    [Dependency] private EntityQuery<ThermalVisionComponent> _legacyThermalQuery = default!;
    [Dependency] private EntityQuery<EyeProtectionComponent> _eyeProtectionQuery = default!;
    [Dependency] private LegacyNightVisionSystem _nightVision = default!;
    [Dependency] private LegacyThermalVisionSystem _thermalVision = default!;

    private readonly FishNightVisionPresentation _presentation = new();
    private FishNightVisionOverlay _night = default!;
    private FishVisionLightOverlay _lightOverlay = default!;
    private FishThermalWorldOverlay _world = default!;
    private FishThermalContactOverlay _contacts = default!;
    private FishNightVisionComponent? _nightSource;
    private FishThermalVisionComponent? _thermalSource;

    internal EntityUid? ThermalViewer { get; private set; }
    internal FishNightVisionPresentation NightPresentation => _presentation;
    internal float ThermalRange { get; private set; }
    internal TimeSpan ThermalActivatedAt { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        _night = new(this, _presentation);
        _lightOverlay = new(this, _presentation);
        _world = new(this);
        _contacts = new(this, EntityManager);
        _overlay.AddOverlay(_night);
        _overlay.AddOverlay(_lightOverlay);
        _overlay.AddOverlay(_world);
        _overlay.AddOverlay(_contacts);
    }

    public override void Shutdown()
    {
        _presentation.Reset();
        ThermalViewer = null;
        _overlay.RemoveOverlay(_night);
        _overlay.RemoveOverlay(_lightOverlay);
        _overlay.RemoveOverlay(_world);
        _overlay.RemoveOverlay(_contacts);
        _night.Dispose();
        _lightOverlay.Dispose();
        _world.Dispose();
        _contacts.Dispose();
        base.Shutdown();
    }

    /// <summary>Excludes occluded light from exposure and halo sampling before the final FOV pass.</summary>
    internal void SetVisibilityMask(ShaderInstance shader, in OverlayDrawArgs args)
    {
        var enabled = args.Viewport.Eye is { DrawLight: true } && _light.Enabled && _light.DrawLighting &&
            _mapQuery.TryComp(_map.GetMapOrInvalid(args.MapId), out var map) && map.LightingEnabled;
        shader.SetParameter("lighting_mask_enabled", enabled);
        shader.SetParameter("VISION_LIGHT_TEXTURE", args.Viewport.LightRenderTarget.Texture);
    }

    /// <summary>
    /// Reports whether a sprite becomes a thermal contact; cloaked bodies keep emitting heat.
    /// </summary>
    internal bool CanDrawThermalContact(Entity<SpriteComponent> sprite) => _contacts.CanDraw(sprite);

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var viewer = _player.LocalEntity;
        FishThermalVisionComponent? thermal = null;
        FishNightVisionComponent? night = null;
        if (viewer != null && !TerminatingOrDeleted(viewer))
        {
            // Защита глаз не мешает собственной оптике прибора: термальная маска или шлем сами могут её давать.
            TryComp(viewer, out thermal);
            if (thermal == null &&
                (!_legacyThermalQuery.HasComp(viewer.Value) || _eyeProtectionQuery.HasComp(viewer.Value)))
                TryComp(viewer, out night);
        }

        var thermalViewer = thermal == null ? null : viewer;
        if (ThermalViewer != thermalViewer || _thermalSource != thermal)
        {
            ThermalActivatedAt = _timing.RealTime;
            _world.ResetExposure();
        }
        ThermalViewer = thermalViewer;
        ThermalRange = thermal is { Range: > 0f } && float.IsFinite(thermal.Range)
            ? Math.Min(thermal.Range, 32f)
            : 0f;
        _thermalSource = thermal;

        _nightVision.SetFishVisionOverride(night != null || thermal != null);
        _thermalVision.SetFishVisionOverride(thermal != null);
        if (_nightSource != night || _presentation.Viewer != (night == null ? null : viewer))
        {
            _presentation.Reset();
            _night.ResetExposure();
        }
        _nightSource = night;
        if (night != null && viewer != null)
        {
            _presentation.Color = night.Color;
            _presentation.SetSource(viewer.Value, viewer.Value, night.LightingColor, _timing.RealTime);
        }
    }
}
