using Content.Shared._Fish.Netrunner;
using Content.Shared.Eye;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Client.Eye;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Graphics;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Fish.Netrunner;

/// <summary>Uses a camera eye for the local viewport while retaining the body as the actor.</summary>
public sealed partial class NetrunnerRemoteViewSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EyeLerpingSystem _eyeLerping = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    private IEye? _remoteEye;
    private EntityUid? _viewer;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(EyeLerpingSystem));
        base.Initialize();
        _overlays.AddOverlay(new NetrunnerCameraOverlay());
        _overlays.AddOverlay(new NetrunnerEyeProgressOverlay());
        CommandBinds.Builder
            .BindBefore(ContentKeyFunctions.ActivateItemInWorld,
                new PointerInputCmdHandler(HandleCancelHackInput),
                typeof(SharedInteractionSystem))
            .Register<NetrunnerRemoteViewSystem>();
    }

    private bool HandleCancelHackInput(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        if (session?.AttachedEntity is not { } user
            || !TryComp<NetrunnerUserComponent>(user, out var state)
            || !state.IsCasting)
            return false;

        state.IsCasting = false;
        if (_timing.IsFirstTimePredicted)
            RaiseNetworkEvent(new NetrunnerCancelHackEvent());
        return true;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var user = _player.LocalEntity;
        UpdateRemoteEyeVisibility(user);
        if (user is { } actor && TryComp<NetrunnerUserComponent>(actor, out var state)
            && state.RemoteEye is { } camera && TryComp<EyeComponent>(camera, out var cameraEye)
            && (MetaData(camera).Flags & MetaDataFlags.Detached) == 0 && cameraEye.Eye is { } eye)
        {
            if (_viewer != camera)
            {
                RestoreEye();
                // Fish-edit: Поворот обзора вычисляем из той же системы координат, что и релейное движение.
                _eyeLerping.AddEye(camera, cameraEye);
                _viewer = camera;
            }
            _remoteEye = eye;
            _eye.CurrentEye = eye;
            return;
        }
        RestoreEye();
    }

    private void UpdateRemoteEyeVisibility(EntityUid? localEntity)
    {
        var curTime = _timing.CurTime;
        var isGhost = localEntity is { } local
            && TryComp<EyeComponent>(local, out var localEye)
            && (localEye.VisibilityMask & (int) VisibilityFlags.Ghost) != 0;
        var query = EntityQueryEnumerator<NetrunnerRemoteEyeComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var viewer, out var sprite))
        {
            var isCasting = viewer.CastEnd > viewer.CastStart && curTime < viewer.CastEnd;
            var isOwnEye = localEntity != null && viewer.User == localEntity.Value;
            var visible = isCasting || isGhost || isOwnEye;
            if (sprite.Visible != visible)
                _sprite.SetVisible((uid, sprite), visible);

            var targetColor = isOwnEye && !isCasting
                ? Color.White.WithAlpha(0.4f)
                : Color.White;
            if (sprite.Color != targetColor)
                _sprite.SetColor((uid, sprite), targetColor);
        }
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<NetrunnerRemoteViewSystem>();
        RestoreEye();
        _overlays.RemoveOverlay<NetrunnerCameraOverlay>();
        _overlays.RemoveOverlay<NetrunnerEyeProgressOverlay>();
        base.Shutdown();
    }

    private void RestoreEye()
    {
        if (_viewer is { } viewer && !Deleted(viewer))
            _eyeLerping.RemoveEye(viewer);
        _viewer = null;
        if (_remoteEye == null)
            return;
        if (_eye.CurrentEye == _remoteEye)
        {
            if (_player.LocalEntity is { } user && TryComp<EyeComponent>(user, out var bodyEye))
                _eye.CurrentEye = bodyEye.Eye;
            else
                _eye.ClearCurrentEye();
        }
        _remoteEye = null;
    }
}
