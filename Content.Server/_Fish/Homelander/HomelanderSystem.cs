using System.Numerics;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._Fish.Homelander;
using Content.Shared.Actions;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
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
    private const float RageViewScale = 1.5f;
    private static readonly TimeSpan ScreamKnockdown = TimeSpan.FromSeconds(2);
    private const string LaserSlot = "fish-homelander-laser";
    private static readonly EntProtoId ScreamAction = "FishActionHomelanderScream";
    private static readonly EntProtoId LaserAction = "FishActionHomelanderLaser";
    private static readonly EntProtoId LaserPrototype = "FishHomelanderEyeGun";
    private static readonly SoundSpecifier ScreamSound = new SoundPathSpecifier("/Audio/_Fish/Homelander/changeling_screech_strong.ogg");

    [Dependency] private SharedHomelanderLaserSystem _laser = default!;
    [Dependency] private SharedContentEyeSystem _contentEye = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
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
        if (!TerminatingOrDeleted(entity))
            SetRageView(entity, false);
        QueueDel(entity.Comp.Laser);
    }

    private void OnScream(Entity<HomelanderComponent> entity, ref HomelanderScreamEvent args)
    {
        if (!args.Handled)
            args.Handled = TryScream(entity, args.Action.Owner);
    }

    public bool CanScream(Entity<HomelanderComponent> entity, EntityUid action)
    {
        return entity.Comp.ScreamAction == action;
    }

    public bool TryScream(Entity<HomelanderComponent> entity, EntityUid action)
    {
        if (!CanScream(entity, action))
            return false;

        _audio.PlayPvs(ScreamSound, entity);

        var origin = _transform.GetMapCoordinates(entity);
        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(origin, ScreamRange, LookupFlags.Uncontained))
        {
            if (target.Owner == entity.Owner || _mobState.IsDead(target, target.Comp) ||
                !_interaction.InRangeUnobstructed(entity.Owner, target.Owner, ScreamRange))
            {
                continue;
            }

            _stun.TryKnockdown(target.Owner, ScreamKnockdown, drop: false);
        }

        return true;
    }

    private void OnToggle(Entity<HomelanderComponent> entity, ref ToggleActionEvent args)
    {
        if (!args.Handled)
            args.Handled = TryToggleLaser(entity, args.Action.Owner);
    }

    public bool CanToggleLaser(Entity<HomelanderComponent> entity, EntityUid action)
    {
        return entity.Comp.LaserAction == action;
    }

    public bool TryToggleLaser(Entity<HomelanderComponent> entity, EntityUid action)
    {
        if (!CanToggleLaser(entity, action))
            return false;

        _appearance.TryGetData<bool>(entity, HomelanderVisuals.LaserActive, out var active);
        _appearance.SetData(entity, HomelanderVisuals.LaserActive, !active);
        _actions.SetToggled(action, !active);
        SetRageView(entity, !active);
        if (!active)
            _popup.PopupEntity(Loc.GetString("fish-homelander-angry"), entity, PopupType.MediumCaution);
        return true;
    }

    private void SetRageView(Entity<HomelanderComponent> entity, bool active)
    {
        if (active)
        {
            if (!TryComp<ContentEyeComponent>(entity, out var contentEye) ||
                !TryComp<EyeComponent>(entity, out var eye))
                return;

            entity.Comp.ViewBeforeRage = (contentEye.TargetZoom, eye.PvsScale);
            _contentEye.SetZoom(entity, Vector2.Max(contentEye.TargetZoom, new Vector2(RageViewScale)),
                ignoreLimits: true, eye: contentEye);
            _eye.SetPvsScale(entity.Owner, MathF.Max(eye.PvsScale, RageViewScale));
        }
        else if (entity.Comp.ViewBeforeRage is { } previous)
        {
            entity.Comp.ViewBeforeRage = null;
            _contentEye.SetZoom(entity, previous.Zoom, ignoreLimits: true);
            _eye.SetPvsScale(entity.Owner, previous.PvsScale);
        }
    }

    private void OnFire(HomelanderFireEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user ||
            !TryComp<HomelanderComponent>(user, out var component) ||
            !TryGetEntity(ev.Coordinates.NetEntity, out var reference))
        {
            return;
        }

        TryGetEntity(ev.Target, out var clicked);
        TryFireLaser((user, component), new EntityCoordinates(reference.Value, ev.Coordinates.Position), clicked);
    }

    public bool CanFireLaser(Entity<HomelanderComponent> entity, EntityCoordinates target, out Entity<GunComponent> gun)
    {
        gun = default;
        if (_containers.IsEntityInContainer(entity) ||
            !_laser.IsActive(entity) ||
            entity.Comp.Laser is not { } laser ||
            !TryComp<GunComponent>(laser, out var gunComponent) ||
            !float.IsFinite(target.Position.X) ||
            !float.IsFinite(target.Position.Y))
        {
            return false;
        }

        var origin = _transform.GetMapCoordinates(entity);
        var destination = _transform.ToMapCoordinates(target, logError: false);
        var distanceSquared = Vector2.DistanceSquared(origin.Position, destination.Position);
        if (origin.MapId == MapId.Nullspace || origin.MapId != destination.MapId ||
            !float.IsFinite(distanceSquared) || distanceSquared <= 0.0001f)
        {
            return false;
        }

        gun = (laser, gunComponent);
        return true;
    }

    public bool TryFireLaser(Entity<HomelanderComponent> entity, EntityCoordinates target, EntityUid? clicked = null)
    {
        return CanFireLaser(entity, target, out var gun) && _guns.AttemptShoot(entity, gun, target, clicked);
    }
}
