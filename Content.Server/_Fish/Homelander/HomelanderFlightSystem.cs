using System.Linq;
using Content.Shared._Fish.Homelander;
using Content.Shared.Actions;
using Content.Shared.Mobs;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Standing;
using Content.Shared.Toggleable;
using Robust.Shared.Audio;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Fish.Homelander;

public sealed partial class HomelanderFlightSystem : EntitySystem
{
    private const int ObstacleMask = (int) (CollisionGroup.MidImpassable | CollisionGroup.LowImpassable);

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HomelanderFlightComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<HomelanderFlightComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<HomelanderFlightComponent, ToggleActionEvent>(OnToggle);
        SubscribeLocalEvent<HomelanderFlightComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnInit(Entity<HomelanderFlightComponent> entity, ref ComponentInit args)
    {
        _actions.AddAction(entity, ref entity.Comp.ActionEntity, entity.Comp.Action, entity);
    }

    private void OnRemove(Entity<HomelanderFlightComponent> entity, ref ComponentRemove args)
    {
        if (TerminatingOrDeleted(entity))
            return;

        SetFlying(entity, false);
        _actions.RemoveAction(entity.Owner, entity.Comp.ActionEntity);
    }

    private void OnToggle(Entity<HomelanderFlightComponent> entity, ref ToggleActionEvent args)
    {
        if (!args.Handled && args.Action.Owner == entity.Comp.ActionEntity)
            args.Handled = TryToggleFlight(entity);
    }

    private void OnMobStateChanged(Entity<HomelanderFlightComponent> entity, ref MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Critical or MobState.Dead)
            SetFlying(entity, false);
    }

    public bool TryToggleFlight(Entity<HomelanderFlightComponent> entity)
    {
        if (!CanToggleFlight(entity))
            return false;

        SetFlying(entity, !entity.Comp.FlightEnabled);
        return true;
    }

    public bool CanToggleFlight(Entity<HomelanderFlightComponent> entity)
    {
        return entity.Comp.FlightEnabled || !_standing.IsDown(entity.Owner);
    }

    private void SetFlying(Entity<HomelanderFlightComponent> entity, bool flying)
    {
        if (entity.Comp.FlightEnabled == flying)
            return;

        entity.Comp.FlightEnabled = flying;
        if (flying)
            TakeOff(entity, AddComp<HomelanderAirborneComponent>(entity));
        else if (TryComp<HomelanderAirborneComponent>(entity, out var airborne))
        {
            Land(entity, airborne);
            RemComp<HomelanderAirborneComponent>(entity);
        }

        _movement.RefreshMovementSpeedModifiers(entity.Owner);
        _movement.RefreshFrictionModifiers(entity.Owner);
        if (entity.Comp.ActionEntity is { } action)
            _actions.SetToggled(action, flying);
        Dirty(entity);
    }

    private void TakeOff(EntityUid uid, HomelanderAirborneComponent airborne)
    {
        airborne.AddedAirMovement = !EnsureComp<CanMoveInAirComponent>(uid, out airborne.CanMoveInAir);
        airborne.AddedFootstep = !EnsureComp<FootstepModifierComponent>(uid, out var footstep);
        airborne.Footstep = footstep;
        airborne.FootstepSound = footstep.FootstepSoundCollection;
        footstep.FootstepSoundCollection = null;
        Dirty(uid, footstep);

        if (TryComp<PhysicsComponent>(uid, out var physics))
            _physics.SetBodyStatus(uid, physics, BodyStatus.InAir);

        if (!TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        foreach (var (id, fixture) in fixtures.Fixtures.ToArray())
        {
            var removed = fixture.CollisionMask & ObstacleMask;
            if (removed == 0)
                continue;

            airborne.RemovedCollisionMasks[id] = (fixture, removed);
            _physics.RemoveCollisionMask(uid, id, fixture, removed, manager: fixtures);
        }
    }

    private void Land(EntityUid uid, HomelanderAirborneComponent airborne)
    {
        if (airborne.AddedAirMovement &&
            TryComp<CanMoveInAirComponent>(uid, out var movement) && movement == airborne.CanMoveInAir)
            RemComp<CanMoveInAirComponent>(uid);

        if (TryComp<FootstepModifierComponent>(uid, out var footstep) &&
            footstep == airborne.Footstep && footstep.FootstepSoundCollection == null)
        {
            if (airborne.AddedFootstep)
                RemComp<FootstepModifierComponent>(uid);
            else
            {
                footstep.FootstepSoundCollection = airborne.FootstepSound;
                Dirty(uid, footstep);
            }
        }

        if (TryComp<PhysicsComponent>(uid, out var physics))
            _physics.SetBodyStatus(uid, physics, BodyStatus.OnGround);

        if (!TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        foreach (var (id, (original, mask)) in airborne.RemovedCollisionMasks)
        {
            if (fixtures.Fixtures.TryGetValue(id, out var fixture) && ReferenceEquals(fixture, original))
                _physics.AddCollisionMask(uid, id, fixture, mask, manager: fixtures);
        }
    }
}

[RegisterComponent]
public sealed partial class HomelanderAirborneComponent : Component
{
    public CanMoveInAirComponent? CanMoveInAir;
    public bool AddedAirMovement;
    public FootstepModifierComponent? Footstep;
    public bool AddedFootstep;
    public SoundSpecifier? FootstepSound;
    public readonly Dictionary<string, (Fixture Fixture, int Mask)> RemovedCollisionMasks = new();
}
