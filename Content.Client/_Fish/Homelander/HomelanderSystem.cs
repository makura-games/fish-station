using System.Numerics;
using Content.Shared._Fish.Homelander;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Client._Fish.Homelander;

public sealed partial class HomelanderSystem : EntitySystem
{
    private static readonly ResPath SpritePath = new("/Textures/_Fish/Homelander/actions.rsi");

    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HomelanderComponent, AppearanceChangeEvent>(OnAppearanceChanged);
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
            _sprite.LayerSetOffset(sprite, layer, new Vector2(0.37f, 0.43f));
        }

        _sprite.LayerSetVisible(sprite, layer, active);
    }
}
