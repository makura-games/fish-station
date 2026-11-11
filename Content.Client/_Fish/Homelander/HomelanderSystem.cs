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
        SubscribeLocalEvent<HomelanderComponent, AppearanceChangeEvent>(OnAppearanceChanged);
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

        var entity = (uid, sprite);
        var lifted = _sprite.LayerMapTryGet(entity, FlightShadowLayer, out var shadow, false);
        if (flying == lifted)
            return;

        _floating.RefreshHomelanderBobbing(uid, flying);

        if (flying)
        {
            shadow = _sprite.AddRsiLayer(entity, "flight-shadow", SpritePath, index: 0);
            _sprite.LayerMapSet(entity, FlightShadowLayer, shadow);
            _sprite.LayerSetOffset(entity, shadow, new Vector2(0f, -0.35f) - FlightLift);
            _sprite.SetOffset(entity, sprite.Offset + FlightLift);
        }
        else
        {
            _sprite.RemoveLayer(entity, shadow);
            _sprite.SetOffset(entity, sprite.Offset - FlightLift);
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

    private void OnAppearanceChanged(Entity<HomelanderComponent> entity, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = (entity.Owner, args.Sprite);
        _appearance.TryGetData<bool>(entity, HomelanderVisuals.LaserActive, out var active, args.Component);
        if (!_sprite.LayerMapTryGet(sprite, HomelanderVisuals.Anger, out var layer, false))
        {
            if (!active)
                return;

            layer = _sprite.LayerMapReserve(sprite, HomelanderVisuals.Anger);
            _sprite.LayerSetRsi(sprite, layer, SpritePath, "anger");
            _sprite.LayerSetOffset(sprite, layer, new Vector2(0.23f, 0.36f));
        }

        _sprite.LayerSetVisible(sprite, layer, active);
    }
}
