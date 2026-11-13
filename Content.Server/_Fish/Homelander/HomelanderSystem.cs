using System.Numerics;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._Fish.Homelander;
using Content.Shared.Actions;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Popups;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared.Toggleable;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Homelander;

public sealed partial class HomelanderSystem : EntitySystem
{
    private const float ScreamRange = 3f;
    private static readonly TimeSpan ScreamKnockdown = TimeSpan.FromSeconds(2);
    private const string LaserSlot = "fish-homelander-laser";
    private static readonly EntProtoId ScreamAction = "FishActionHomelanderScream";
    private static readonly EntProtoId LaserAction = "FishActionHomelanderLaser";
    private static readonly EntProtoId LaserPrototype = "FishHomelanderEyeGun";
    private static readonly SoundSpecifier ScreamSound = new SoundPathSpecifier("/Audio/_Fish/Homelander/changeling_screech_strong.ogg");

    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private GunSystem _guns = default!;
    [Dependency] private SunriseHumanoidMarkingSystem _markings = default!;

    private const string Hair = "HumanHairCombed";
    private static readonly Color HairColor = Color.FromHex("#C2A476");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HomelanderComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HomelanderComponent, MapInitEvent>(OnMapInit, after: [typeof(InitialBodySystem)]);
        SubscribeLocalEvent<HomelanderComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<HomelanderComponent, HomelanderScreamEvent>(OnScream);
        SubscribeLocalEvent<HomelanderComponent, ToggleActionEvent>(OnToggle);
        SubscribeNetworkEvent<HomelanderFireEvent>(OnFire);
    }

    private void OnStartup(Entity<HomelanderComponent> entity, ref ComponentStartup args)
    {
        _actions.AddAction(entity, ref entity.Comp.ScreamAction, ScreamAction, entity);
        _actions.AddAction(entity, ref entity.Comp.LaserAction, LaserAction, entity);
        _containers.EnsureContainer<ContainerSlot>(entity, LaserSlot);
        TrySpawnInContainer(LaserPrototype, entity, LaserSlot, out entity.Comp.Laser);
    }

    private void OnMapInit(Entity<HomelanderComponent> entity, ref MapInitEvent args)
    {
        if (_markings.SetMarkingId(entity, HumanoidVisualLayers.Hair, 0, Hair))
            _markings.SetAllMarkingColors(entity, HumanoidVisualLayers.Hair, HairColor);
        else
            _markings.AddMarking(entity, HumanoidVisualLayers.Hair, Hair, HairColor);
    }

    private void OnShutdown(Entity<HomelanderComponent> entity, ref ComponentShutdown args)
    {
        _actions.RemoveAction(entity.Owner, entity.Comp.ScreamAction);
        _actions.RemoveAction(entity.Owner, entity.Comp.LaserAction);
        _appearance.SetData(entity, HomelanderVisuals.LaserActive, false);
        QueueDel(entity.Comp.Laser);
    }

    private void OnScream(EntityUid uid, HomelanderComponent component, HomelanderScreamEvent args)
    {
        if (args.Handled || args.Action.Owner != component.ScreamAction)
            return;

        _audio.PlayPvs(ScreamSound, uid);

        var origin = _transform.GetMapCoordinates(uid);
        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(origin, ScreamRange, LookupFlags.Uncontained))
        {
            if (target.Owner == uid || _mobState.IsDead(target, target.Comp) ||
                !_interaction.InRangeUnobstructed(uid, target.Owner, ScreamRange))
            {
                continue;
            }

            _stun.TryKnockdown(target.Owner, ScreamKnockdown, drop: false);
        }

        args.Handled = true;
    }

    private void OnToggle(Entity<HomelanderComponent> entity, ref ToggleActionEvent args)
    {
        if (args.Handled || args.Action.Owner != entity.Comp.LaserAction)
            return;

        _appearance.TryGetData<bool>(entity, HomelanderVisuals.LaserActive, out var active);
        _appearance.SetData(entity, HomelanderVisuals.LaserActive, !active);
        _actions.SetToggled(args.Action.Owner, !active);
        if (!active)
            _popup.PopupEntity(Loc.GetString("fish-homelander-angry"), entity, PopupType.MediumCaution);
        args.Handled = true;
    }

    private void OnFire(HomelanderFireEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user ||
            !TryComp<HomelanderComponent>(user, out var component) ||
            _containers.IsEntityInContainer(user) ||
            !_appearance.TryGetData<bool>(user, HomelanderVisuals.LaserActive, out var active) ||
            !active ||
            component.Laser is not { } laser ||
            !TryComp<GunComponent>(laser, out var gun) ||
            !TryGetEntity(ev.Coordinates.NetEntity, out var reference) ||
            !float.IsFinite(ev.Coordinates.Position.X) ||
            !float.IsFinite(ev.Coordinates.Position.Y))
        {
            return;
        }

        var target = new EntityCoordinates(reference.Value, ev.Coordinates.Position);
        var origin = _transform.GetMapCoordinates(user);
        var destination = _transform.ToMapCoordinates(target);
        if (origin.MapId == MapId.Nullspace || origin.MapId != destination.MapId ||
            !float.IsFinite(destination.Position.X) || !float.IsFinite(destination.Position.Y) ||
            Vector2.DistanceSquared(origin.Position, destination.Position) < 0.0001f)
        {
            return;
        }

        TryGetEntity(ev.Target, out var clicked);
        _guns.AttemptShoot(user, laser, gun, target, clicked);
    }
}
