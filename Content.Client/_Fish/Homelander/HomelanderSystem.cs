using System.Numerics;
using Content.Client.Gravity;
using Content.Shared._Fish.Homelander;
using Content.Client.ContextMenu.UI;
using Content.Client.Gameplay;
using Content.Shared.Gravity;
using Content.Shared.Speech;
using Robust.Client.State;
using Robust.Shared.GameStates;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Client._Fish.Homelander;

public sealed partial class HomelanderSystem : EntitySystem
{
    private const float FlightTransitionSeconds = 0.3f;
    private const float HoverPeriod = 1.4f;
    private const float HoverAmplitude = 0.06f;
    private const string FlightShadowLayer = "fish-homelander-flight-shadow";
    private static readonly Vector2 FlightLift = new(0f, HomelanderComponent.FlightVisualLift);
    private static readonly ResPath SpritePath = new("/Textures/_Fish/Homelander/actions.rsi");

    [Dependency] private SharedHomelanderLaserSystem _laser = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private FloatingVisualizerSystem _floating = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private IStateManager _stateManager = default!;
    [Dependency] private InputSystem _input = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HomelanderFlightComponent, ComponentStartup>(OnFlightStartup);
        SubscribeLocalEvent<HomelanderFlightComponent, ComponentShutdown>(OnFlightShutdown);
        SubscribeLocalEvent<HomelanderFlightComponent, AfterAutoHandleStateEvent>(OnFlightState);
        SubscribeLocalEvent<HomelanderFlightComponent, FloatingAnimationAttemptEvent>(OnFloatAttempt);
        SubscribeLocalEvent<HomelanderLiftComponent, FloatingAnimationAttemptEvent>(OnLiftFloatAttempt);

        CommandBinds.Builder
            .BindBefore(EngineKeyFunctions.UseSecondary, new PointerInputCmdHandler(OnSecondaryUse, outsidePrediction: true),
                typeof(EntityMenuUIController))
            .Register<HomelanderSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<HomelanderSystem>();
        base.Shutdown();
    }

    private bool OnSecondaryUse(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        return !_input.Predicted && _stateManager.CurrentState is GameplayStateBase &&
            TryFire(args.Session?.AttachedEntity, args.Coordinates, args.EntityUid);
    }

    private void OnFlightStartup(Entity<HomelanderFlightComponent> entity, ref ComponentStartup args)
    {
        UpdateFlightVisual(entity, entity.Comp.FlightEnabled);
    }

    private void OnFlightShutdown(Entity<HomelanderFlightComponent> entity, ref ComponentShutdown args)
    {
        UpdateFlightVisual(entity, false);
    }

    private void OnFlightState(Entity<HomelanderFlightComponent> entity, ref AfterAutoHandleStateEvent args)
    {
        UpdateFlightVisual(entity, entity.Comp.FlightEnabled);
    }

    private void OnFloatAttempt(Entity<HomelanderFlightComponent> entity, ref FloatingAnimationAttemptEvent args)
    {
        if (entity.Comp.FlightEnabled)
            args.Cancelled = true;
    }

    private void OnLiftFloatAttempt(Entity<HomelanderLiftComponent> entity, ref FloatingAnimationAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void UpdateFlightVisual(EntityUid uid, bool flying)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!TryComp<HomelanderLiftComponent>(uid, out var lift))
        {
            if (!flying)
                return;

            if (TryComp<FloatingVisualsComponent>(uid, out var floating))
                _floating.FloatAnimation(uid, floating.Offset, floating.AnimationKey, floating.AnimationTime, stop: true);
            lift = EnsureComp<HomelanderLiftComponent>(uid);
            lift.GroundOffset = sprite.Offset;
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
            var step = frameTime / FlightTransitionSeconds;
            lift.Progress = Math.Clamp(lift.Progress + (lift.Flying ? step : -step), 0f, 1f);
            var eyeRotation = _eye.CurrentEye.Rotation;
            var facing = _transform.GetWorldRotation(uid) + eyeRotation;
            var renderRotation = sprite.NoRotation ? Angle.Zero :
                sprite.SnapCardinals ? facing - facing.RoundToCardinalAngle() : facing;
            lift.HoverPhase = (lift.HoverPhase + frameTime / HoverPeriod) % 1f;

            var progress = lift.Progress * lift.Progress * (3f - 2f * lift.Progress);
            var hover = HoverAmplitude * (0.5f - 0.5f * MathF.Cos(lift.HoverPhase * MathF.Tau));
            var height = (FlightLift + new Vector2(0f, hover)) * progress;
            var entity = (uid, sprite);
            lift.WorldTextOffset = (-eyeRotation).RotateVec(height);
            _sprite.SetOffset(entity, lift.GroundOffset + (-renderRotation).RotateVec(height));

            if (TryComp<SpeechComponent>(uid, out var speech))
            {
                if (lift.Speech != speech)
                {
                    lift.Speech = speech;
                    lift.BaseSpeechOffset = speech.SpeechBubbleOffset;
                }

                speech.SpeechBubbleOffset = lift.BaseSpeechOffset + height.Y;
            }
            var rotation = sprite.Rotation;
            var shadowOffset = (-renderRotation).RotateVec(new Vector2(0f, -0.35f)) - sprite.Offset;
            _sprite.LayerSetOffset(entity, FlightShadowLayer, (-rotation).RotateVec(shadowOffset));
            _sprite.LayerSetRotation(entity, FlightShadowLayer, -rotation - renderRotation);
            _sprite.LayerSetColor(entity, FlightShadowLayer, Color.White.WithAlpha(progress * (1f - hover * 4f)));

            if (!lift.Flying && lift.Progress == 0f)
            {
                if (_sprite.LayerMapTryGet(entity, FlightShadowLayer, out var shadow, false))
                    _sprite.RemoveLayer(entity, shadow);

                RemComp<HomelanderLiftComponent>(uid);
                if (!TerminatingOrDeleted(uid) && _gravity.IsWeightless(uid) &&
                    TryComp<FloatingVisualsComponent>(uid, out var floating))
                    _floating.FloatAnimation(uid, floating.Offset, floating.AnimationKey, floating.AnimationTime);
            }
        }
    }

    public bool TryGetFlightPopupOffset(EntityUid uid, Angle eyeRotation, out Vector2 offset)
    {
        offset = default;
        if (!TryComp<HomelanderLiftComponent>(uid, out var lift))
            return false;

        offset = lift.WorldTextOffset + (-eyeRotation).RotateVec(new Vector2(0f, 0.65f));
        return true;
    }

    public bool IsLaserActive(EntityUid? user)
    {
        return _laser.IsActive(user);
    }

    private bool TryFire(EntityUid? user, EntityCoordinates coordinates, EntityUid target)
    {
        if (!IsLaserActive(user))
            return false;

        if (coordinates.IsValid(EntityManager))
            RaiseNetworkEvent(new HomelanderFireEvent(GetNetCoordinates(coordinates), GetNetEntity((EntityUid?) target)));

        return true;
    }
}
