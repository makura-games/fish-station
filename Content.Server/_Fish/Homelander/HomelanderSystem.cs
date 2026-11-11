using System.Numerics;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._Fish.Homelander;
using Content.Shared.Actions;
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
    private const string LaserSlot = "fish-homelander-laser";
    private static readonly EntProtoId ScreamAction = "FishActionHomelanderScream";
    private static readonly EntProtoId LaserAction = "FishActionHomelanderLaser";
    private static readonly EntProtoId LaserPrototype = "FishHomelanderEyeGun";
    private static readonly SoundSpecifier ScreamSound = new SoundPathSpecifier("/Audio/_Fish/Homelander/changeling_screech_strong.ogg");

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private GunSystem _guns = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HomelanderComponent, ComponentStartup>(OnStartup);
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
        args.Handled = true;
    }

    private void OnToggle(Entity<HomelanderComponent> entity, ref ToggleActionEvent args)
    {
        if (args.Handled || args.Action.Owner != entity.Comp.LaserAction)
            return;

        _appearance.TryGetData<bool>(entity, HomelanderVisuals.LaserActive, out var active);
        _appearance.SetData(entity, HomelanderVisuals.LaserActive, !active);
        _actions.SetToggled(args.Action.Owner, !active);
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

        _guns.AttemptShoot(user, laser, gun, target);
    }
}
