using System.Numerics;
using Content.Client.Gravity;
using Content.Shared._Fish.Homelander;
using Content.Shared._Sunrise.Abilities.Milira;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;

namespace Content.Client._Fish.Homelander;

public sealed partial class HomelanderSystem : EntitySystem
{
    private const float FlightTransitionSeconds = 0.3f;
    private const float MaxLeanRadians = MathF.PI / 3f;
    private const float SpeedForFullLean = 6f;
    private const string FlightShadowLayer = "fish-homelander-flight-shadow";
    private static readonly Vector2 FlightLift = new(0f, HomelanderComponent.FlightVisualLift);
    private static readonly ResPath SpritePath = new("/Textures/_Fish/Homelander/actions.rsi");

    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;
    [Dependency] private FloatingVisualizerSystem _floating = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HomelanderComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HomelanderComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<HomelanderComponent> entity, ref ComponentStartup args)
    {
        if (TryComp<WingFlightComponent>(entity, out var flight) && flight.FlightEnabled)
            UpdateFlightVisual(entity, true);
    }

    private void OnShutdown(Entity<HomelanderComponent> entity, ref ComponentShutdown args)
    {
        UpdateFlightVisual(entity, false);
    }

    internal void RefreshFlightVisual(EntityUid uid, bool flying)
    {
        if (HasComp<HomelanderComponent>(uid))
            UpdateFlightVisual(uid, flying);
    }

    private void UpdateFlightVisual(EntityUid uid, bool flying)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!TryComp<HomelanderLiftComponent>(uid, out var lift))
        {
            if (!flying)
                return;

            _floating.RefreshHomelanderBobbing(uid, true);
            lift = EnsureComp<HomelanderLiftComponent>(uid);
            lift.GroundOffset = sprite.Offset;
            lift.GroundRotation = sprite.Rotation;
            var entity = (uid, sprite);
            var shadow = _sprite.AddRsiLayer(entity, "flight-shadow", SpritePath, index: 0);
            _sprite.LayerMapSet(entity, FlightShadowLayer, shadow);
        }

        lift.Flying = flying;
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<HomelanderLiftComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var lift, out var sprite))
        {
            var previousProgress = lift.Progress;
            var previousLean = lift.Lean;
            var step = frameTime / FlightTransitionSeconds;
            lift.Progress = Math.Clamp(lift.Progress + (lift.Flying ? step : -step), 0f, 1f);
            var targetLean = lift.Flying ? GetTargetLean(uid) : 0f;
            lift.Lean = MathHelper.Lerp(lift.Lean, targetLean, 1f - MathF.Exp(-12f * frameTime));
            if (MathF.Abs(lift.Lean - targetLean) < 0.001f)
                lift.Lean = targetLean;

            if (lift.Flying && previousProgress == lift.Progress && previousLean == lift.Lean)
                continue;

            var progress = lift.Progress * lift.Progress * (3f - 2f * lift.Progress);
            var height = FlightLift * progress;
            var entity = (uid, sprite);
            _sprite.SetOffset(entity, lift.GroundOffset + height);
            var rotation = lift.GroundRotation + new Angle(lift.Lean * progress);
            _sprite.SetRotation(entity, rotation);
            var shadowOffset = new Vector2(0f, -0.35f) - lift.GroundOffset - height;
            _sprite.LayerSetOffset(entity, FlightShadowLayer, (-rotation).RotateVec(shadowOffset));
            _sprite.LayerSetRotation(entity, FlightShadowLayer, -rotation);
            _sprite.LayerSetColor(entity, FlightShadowLayer, Color.White.WithAlpha(progress));

            if (!lift.Flying && lift.Progress == 0f)
            {
                if (_sprite.LayerMapTryGet(entity, FlightShadowLayer, out var shadow, false))
                    _sprite.RemoveLayer(entity, shadow);

                RemComp<HomelanderLiftComponent>(uid);
                _floating.RefreshHomelanderBobbing(uid, false);
            }
        }
    }

    private float GetTargetLean(EntityUid uid)
    {
        if (!_physicsQuery.TryComp(uid, out var body))
            return 0f;

        // Скорость относительно грида, чтобы движение шаттла не наклоняло Патриота.
        var parent = Transform(uid).ParentUid;
        var rotation = (parent.IsValid() ? _transform.GetWorldRotation(parent) : Angle.Zero) + _eye.CurrentEye.Rotation;
        var velocity = rotation.RotateVec(body.LinearVelocity);
        return -Math.Clamp(velocity.X / SpeedForFullLean, -1f, 1f) * MaxLeanRadians;
    }

    public bool IsLaserActive(EntityUid? user)
    {
        return user is { } uid &&
            HasComp<HomelanderComponent>(uid) &&
            _appearance.TryGetData<bool>(uid, HomelanderVisuals.LaserActive, out var active) &&
            active;
    }

    public bool TryFire(EntityUid? user, EntityCoordinates coordinates, EntityUid target)
    {
        if (!IsLaserActive(user))
            return false;

        if (coordinates.IsValid(EntityManager))
            RaiseNetworkEvent(new HomelanderFireEvent(GetNetCoordinates(coordinates), GetNetEntity((EntityUid?) target)));

        return true;
    }
}
