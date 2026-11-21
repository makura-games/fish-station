using Content.Shared._Fish.Netrunner;
using Content.Client.Eye;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Graphics;

namespace Content.Client._Fish.Netrunner;

/// <summary>Uses a camera eye for the local viewport while retaining the body as the actor.</summary>
public sealed partial class NetrunnerRemoteViewSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private EyeLerpingSystem _eyeLerping = default!;
    private IEye? _remoteEye;
    private EntityUid? _viewer;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(EyeLerpingSystem));
        base.Initialize();
        _overlays.AddOverlay(new NetrunnerCameraOverlay());
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var user = _player.LocalEntity;
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

    public override void Shutdown()
    {
        RestoreEye();
        _overlays.RemoveOverlay<NetrunnerCameraOverlay>();
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
