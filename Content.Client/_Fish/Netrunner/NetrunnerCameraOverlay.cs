using System.Numerics;
using Content.Client.Graphics;
using Content.Shared._Fish.Netrunner;
using Content.Shared.Silicons.StationAi;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Fish.Netrunner;

/// <summary>Masks the remote viewport to camera-covered tiles without granting station AI interaction.</summary>
public sealed partial class NetrunnerCameraOverlay : Overlay
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<ShaderPrototype> CameraStaticShader = "CameraStatic";
    private static readonly ProtoId<ShaderPrototype> StencilMaskShader = "StencilMask";
    private static readonly ProtoId<ShaderPrototype> StencilDrawShader = "StencilDraw";

    private readonly OverlayResourceCache<CachedResources> _resources = new();
    private readonly ShaderInstance _static;
    private readonly ShaderInstance _mask;
    private readonly ShaderInstance _draw;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public NetrunnerCameraOverlay()
    {
        IoCManager.InjectDependencies(this);
        _static = _proto.Index(CameraStaticShader).Instance();
        _mask = _proto.Index(StencilMaskShader).Instance();
        _draw = _proto.Index(StencilDrawShader).Instance();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.Viewport.Eye == _eye.CurrentEye
            && _entities.TryGetComponent(_player.LocalEntity, out NetrunnerUserComponent? user)
            && user.RemoteEye != null;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var resources = _resources.GetForViewport(args.Viewport, static _ => new CachedResources());
        if (resources.Mask?.Texture.Size != args.Viewport.Size)
        {
            resources.Mask?.Dispose();
            resources.Static?.Dispose();
            var format = new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb);
            resources.Mask = _clyde.CreateRenderTarget(args.Viewport.Size, format, name: "netrunner-camera-mask");
            resources.Static = _clyde.CreateRenderTarget(args.Viewport.Size, format, name: "netrunner-camera-static");
            resources.NextUpdate = TimeSpan.Zero;
        }
        var handle = args.WorldHandle;
        var bounds = args.WorldBounds;
        var inverse = args.Viewport.GetWorldToLocalMatrix();
        var matrix = Matrix3x2.Identity;
        ushort tileSize = 1;
        if (_entities.TryGetComponent(_player.LocalEntity, out NetrunnerUserComponent? user)
            && _entities.TryGetComponent(user.RemoteEye, out NetrunnerRemoteEyeComponent? viewer)
            && _entities.TryGetComponent(user.RemoteEye, out TransformComponent? transform)
            && _entities.TryGetComponent(transform.GridUid, out MapGridComponent? grid)
            && _entities.TryGetComponent(transform.GridUid, out BroadphaseComponent? broadphase))
        {
            var gridUid = transform!.GridUid!.Value;
            var transforms = _entities.System<SharedTransformSystem>();
            matrix = Matrix3x2.Multiply(transforms.GetWorldMatrix(gridUid), inverse);
            tileSize = grid!.TileSize;
            if (resources.NextUpdate <= _timing.CurTime || resources.Grid != gridUid)
            {
                resources.Grid = gridUid;
                resources.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(0.1);
                _entities.System<StationAiVisionSystem>().GetNetrunnerView(
                    (gridUid, broadphase!, grid), bounds, viewer!.Station, resources.VisibleTiles);
            }
        }
        else
        {
            resources.VisibleTiles.Clear();
            resources.NextUpdate = TimeSpan.Zero;
        }

        // Fish-edit: Закрываем шумом все клетки вне покрытия, включая соседние станции и пустоту.
        handle.UseShader(null);
        handle.RenderInRenderTarget(resources.Mask!, () =>
        {
            handle.SetTransform(matrix);
            var lookup = _entities.System<EntityLookupSystem>();
            foreach (var tile in resources.VisibleTiles)
                handle.DrawRect(lookup.GetLocalBounds(tile, tileSize), Color.White);
        }, Color.Transparent);
        handle.RenderInRenderTarget(resources.Static!, () =>
        {
            handle.SetTransform(inverse);
            handle.UseShader(_static);
            handle.DrawRect(bounds, Color.White);
        }, Color.Black);
        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(_mask);
        handle.DrawTextureRect(resources.Mask!.Texture, bounds);
        handle.UseShader(_draw);
        handle.DrawTextureRect(resources.Static!.Texture, bounds);
        handle.UseShader(null);
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();
        base.DisposeBehavior();
    }

    private sealed class CachedResources : IDisposable
    {
        public IRenderTexture? Mask;
        public IRenderTexture? Static;
        public EntityUid? Grid;
        public TimeSpan NextUpdate;
        public readonly HashSet<Vector2i> VisibleTiles = new();

        public void Dispose()
        {
            Mask?.Dispose();
            Static?.Dispose();
        }
    }
}
