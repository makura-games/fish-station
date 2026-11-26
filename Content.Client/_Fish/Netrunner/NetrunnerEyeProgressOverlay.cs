using System.Numerics;
using Content.Client.UserInterface.Systems;
using Content.Shared._Fish.Netrunner;
using Content.Shared.Eye;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Fish.Netrunner;

/// <summary>Draws script cast progress above visible netrunner eyes, strictly hidden from viewers without VisibilityFlags.Netrunner.</summary>
public sealed partial class NetrunnerEyeProgressOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";

    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private readonly SharedTransformSystem _transform;
    private readonly ProgressColorSystem _progressColor;
    private readonly Texture _barTexture;
    private readonly ShaderInstance _unshadedShader;

    private const float StartX = 2f;
    private const float EndX = 22f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public NetrunnerEyeProgressOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entities.System<SharedTransformSystem>();
        _progressColor = _entities.System<ProgressColorSystem>();
        var sprite = new SpriteSpecifier.Rsi(new("/Textures/Interface/Misc/progress_bar.rsi"), "icon");
        _barTexture = _entities.System<SpriteSystem>().Frame0(sprite);
        _unshadedShader = _proto.Index(UnshadedShader).Instance();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local)
            return false;

        if (_entities.TryGetComponent(local, out NetrunnerUserComponent? user)
            && user.RemoteEye is { } remote
            && _entities.TryGetComponent(remote, out EyeComponent? remoteEye)
            && (remoteEye.VisibilityMask & (int) VisibilityFlags.Netrunner) != 0)
            return true;

        return _entities.TryGetComponent(local, out EyeComponent? eye)
            && (eye.VisibilityMask & (int) VisibilityFlags.Netrunner) != 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var rotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var rotationMatrix = Matrix3Helpers.CreateRotation(-rotation);
        var bounds = args.WorldAABB.Enlarged(5f);
        var curTime = _timing.CurTime;
        var xformQuery = _entities.GetEntityQuery<TransformComponent>();

        handle.UseShader(_unshadedShader);

        var enumerator = _entities.AllEntityQueryEnumerator<NetrunnerRemoteEyeComponent, TransformComponent>();
        while (enumerator.MoveNext(out _, out var eye, out var xform))
        {
            if (xform.MapID != args.MapId || eye.CastEnd <= eye.CastStart || curTime >= eye.CastEnd)
                continue;

            var worldPosition = _transform.GetWorldPosition(xform, xformQuery);
            if (!bounds.Contains(worldPosition))
                continue;

            var total = (eye.CastEnd - eye.CastStart).TotalSeconds;
            if (total <= 0)
                continue;

            var progress = Math.Clamp((float) ((curTime - eye.CastStart).TotalSeconds / total), 0f, 1f);
            var worldMatrix = Matrix3Helpers.CreateTranslation(worldPosition);
            handle.SetTransform(Matrix3x2.Multiply(rotationMatrix, worldMatrix));

            var yOffset = 0.65f;
            var position = new Vector2(-_barTexture.Width / 2f / EyeManager.PixelsPerMeter, yOffset);
            handle.DrawTexture(_barTexture, position);

            var xProgress = (EndX - StartX) * progress + StartX;
            var box = new Box2(new Vector2(StartX, 3f) / EyeManager.PixelsPerMeter,
                new Vector2(xProgress, 4f) / EyeManager.PixelsPerMeter);
            box = box.Translated(position);
            handle.DrawRect(box, _progressColor.GetProgressColor(progress));
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }
}
