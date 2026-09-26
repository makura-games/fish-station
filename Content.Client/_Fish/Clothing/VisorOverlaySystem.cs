using Content.Shared._Fish.Clothing;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.GameStates;
using Robust.Shared.Player;

namespace Content.Client._Fish.Clothing;

/// <summary>
/// Manages the visor overlay for the local player.
/// </summary>
public sealed partial class VisorOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _player = default!;

    private VisorOverlay _overlay = default!;
    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VisorOverlayComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VisorOverlayComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VisorOverlayComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<VisorOverlayComponent, LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<VisorOverlayComponent, LocalPlayerDetachedEvent>(OnDetached);

        _overlay = new VisorOverlay();
    }

    public override void Shutdown()
    {
        ForceDisableOverlay();
        base.Shutdown();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_overlayManager.HasOverlay<VisorOverlay>())
            return;

        if (!_enabled && _overlay.IsFullyTransparent)
            _overlayManager.RemoveOverlay(_overlay);
    }

    private void OnStartup(Entity<VisorOverlayComponent> ent, ref ComponentStartup args)
    {
        if (_player.LocalEntity == ent.Owner)
            EnableOverlay(ent.Comp);
    }

    private void OnShutdown(Entity<VisorOverlayComponent> ent, ref ComponentShutdown args)
    {
        if (_player.LocalEntity == ent.Owner)
            DisableOverlay(ent.Comp);
    }

    private void OnAfterState(Entity<VisorOverlayComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_player.LocalEntity == ent.Owner)
            EnableOverlay(ent.Comp);
    }

    private void OnAttached(Entity<VisorOverlayComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        EnableOverlay(ent.Comp);
    }

    private void OnDetached(Entity<VisorOverlayComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        ForceDisableOverlay();
    }

    private void EnableOverlay(VisorOverlayComponent component)
    {
        _enabled = true;
        _overlay.Configure(component);
        _overlay.SetEnabled(true);

        if (!_overlayManager.HasOverlay<VisorOverlay>())
            _overlayManager.AddOverlay(_overlay);
    }

    private void DisableOverlay(VisorOverlayComponent component)
    {
        _enabled = false;
        _overlay.Configure(component);
        _overlay.SetEnabled(false);
    }

    private void ForceDisableOverlay()
    {
        _enabled = false;
        _overlay.SetEnabled(false, immediate: true);
        _overlayManager.RemoveOverlay(_overlay);
    }
}
