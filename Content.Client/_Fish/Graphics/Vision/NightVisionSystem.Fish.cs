using Content.Client._Fish.Graphics.Vision;
using Content.Shared._Fish.Vision;
using Content.Shared.Overlays;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Client.NightVision;

public sealed partial class NightVisionSystem
{
    [Dependency] private ILightManager _light = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private EntityQuery<MapComponent> _mapQuery = default!;

    private FishNaturalNightVisionOverlay _fishNaturalOverlay = default!;

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
            Deactivate(_player.LocalSession?.AttachedEntity);
        }
        else if (_player.LocalSession?.AttachedEntity is { } viewer)
        {
            RefreshOverlay(viewer);
        }
    }

    private void InitializeFishNaturalVision() => _fishNaturalOverlay = new(this);

    private bool TryUpdateFishNaturalVision(EntityUid viewer, NightVisionComponent source)
    {
        if (!TryComp<FishNaturalNightVisionComponent>(viewer, out var profile) ||
            profile.LifeStage > ComponentLifeStage.Running ||
            !TryComp<NightVisionComponent>(viewer, out var innate) || innate != source)
        {
            DeactivateFishNaturalVision();
            return false;
        }

        _overlayMan.RemoveOverlay(_overlay);
        _fishNaturalOverlay.SetSource(viewer, profile);
        if (!_overlayMan.HasOverlay<FishNaturalNightVisionOverlay>())
            _overlayMan.AddOverlay(_fishNaturalOverlay);
        return true;
    }

    private void DeactivateFishNaturalVision()
    {
        if (_overlayMan.HasOverlay<FishNaturalNightVisionOverlay>())
            _overlayMan.RemoveOverlay(_fishNaturalOverlay);
        _fishNaturalOverlay.Reset();
    }

    [SubscribeLocalEvent]
    private void OnFishNaturalState(Entity<FishNaturalNightVisionComponent> ent, ref AfterAutoHandleStateEvent args) =>
        RefreshOverlay(ent);

    [SubscribeLocalEvent]
    private void OnFishNaturalShutdown(Entity<FishNaturalNightVisionComponent> ent, ref ComponentShutdown args) =>
        RefreshOverlay(ent);

    internal bool IsFishNaturalLightingEnabled(in OverlayDrawArgs args) =>
        args.Viewport.Eye is { DrawLight: true } && _light.Enabled && _light.DrawLighting &&
        _mapQuery.TryComp(_map.GetMapOrInvalid(args.MapId), out var map) && map.LightingEnabled;

    private void ShutdownFishNaturalVision()
    {
        DeactivateFishNaturalVision();
        _fishNaturalOverlay.Dispose();
    }
}
