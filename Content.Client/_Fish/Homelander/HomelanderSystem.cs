using System.Numerics;
using Content.Client.Gravity;
using Content.Shared._Fish.Homelander;
using Content.Shared._Sunrise.Abilities.Milira;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Client._Fish.Homelander;

public sealed partial class HomelanderSystem : EntitySystem
{
    private const string FlightShadowLayer = "fish-homelander-flight-shadow";
    private static readonly Vector2 FlightLift = new(0f, HomelanderComponent.FlightVisualLift);
    private static readonly ResPath SpritePath = new("/Textures/_Fish/Homelander/actions.rsi");

    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;
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
            if (lift.Flying && lift.Progress >= 1f)
                continue;
            var step = frameTime / 0.3f;
            lift.Progress = Math.Clamp(lift.Progress + (lift.Flying ? step : -step), 0f, 1f);
            var progress = lift.Progress * lift.Progress * (3f - 2f * lift.Progress);
            var height = FlightLift * progress;
            var entity = (uid, sprite);
            _sprite.SetOffset(entity, lift.GroundOffset + height);
            _sprite.LayerSetOffset(entity, FlightShadowLayer, new Vector2(0f, -0.35f) - lift.GroundOffset - height);
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
