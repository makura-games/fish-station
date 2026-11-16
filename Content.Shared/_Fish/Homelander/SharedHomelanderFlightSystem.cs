using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;

namespace Content.Shared._Fish.Homelander;

public sealed partial class SharedHomelanderFlightSystem : EntitySystem
{
    [Dependency] private StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HomelanderFlightComponent, CanWeightlessMoveEvent>(OnCanWeightlessMove);
        SubscribeLocalEvent<HomelanderFlightComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<HomelanderFlightComponent, RefreshFrictionModifiersEvent>(OnRefreshFriction);
        SubscribeLocalEvent<HomelanderFlightComponent, DownAttemptEvent>(OnDownAttempt);
        SubscribeLocalEvent<HomelanderFlightComponent, KnockDownAttemptEvent>(OnKnockDownAttempt);
        SubscribeLocalEvent<HomelanderFlightComponent, DownedEvent>(OnDowned);
        SubscribeLocalEvent<HomelanderFlightComponent, KnockedDownEvent>(OnKnockedDown);
    }

    private void OnCanWeightlessMove(Entity<HomelanderFlightComponent> entity, ref CanWeightlessMoveEvent args)
    {
        if (!entity.Comp.FlightEnabled)
            return;

        args.CanMove = true;
        args.UseGroundMovement = true;
    }

    private void OnRefreshSpeed(Entity<HomelanderFlightComponent> entity, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (entity.Comp.FlightEnabled)
            args.ModifySpeed(entity.Comp.SpeedModifier);
    }

    private void OnRefreshFriction(Entity<HomelanderFlightComponent> entity, ref RefreshFrictionModifiersEvent args)
    {
        if (entity.Comp.FlightEnabled)
            args.ModifyFriction(entity.Comp.FrictionModifier);
    }

    private void OnDownAttempt(Entity<HomelanderFlightComponent> entity, ref DownAttemptEvent args)
    {
        if (entity.Comp.FlightEnabled)
            args.Cancel();
    }

    private void OnKnockDownAttempt(Entity<HomelanderFlightComponent> entity, ref KnockDownAttemptEvent args)
    {
        if (entity.Comp.FlightEnabled)
            args.Cancelled = true;
    }

    private void OnDowned(Entity<HomelanderFlightComponent> entity, ref DownedEvent args)
    {
        if (entity.Comp.FlightEnabled)
            _standing.Stand(entity, force: true);
    }

    private void OnKnockedDown(Entity<HomelanderFlightComponent> entity, ref KnockedDownEvent args)
    {
        if (!entity.Comp.FlightEnabled)
            return;

        RemComp<KnockedDownComponent>(entity);
        _standing.Stand(entity, force: true);
    }
}
