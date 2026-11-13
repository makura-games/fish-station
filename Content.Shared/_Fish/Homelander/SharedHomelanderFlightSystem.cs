using Content.Shared._Sunrise.Abilities.Milira;
using Content.Shared.Movement.Events;

namespace Content.Shared._Fish.Homelander;

public sealed partial class SharedHomelanderFlightSystem : EntitySystem
{
    [Dependency] private EntityQuery<WingFlightComponent> _flightQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HomelanderComponent, CanWeightlessMoveEvent>(OnCanWeightlessMove);
    }

    private void OnCanWeightlessMove(Entity<HomelanderComponent> entity, ref CanWeightlessMoveEvent args)
    {
        if (_flightQuery.TryComp(entity, out var flight) && flight.FlightEnabled)
        {
            args.CanMove = true;
            args.UseGroundMovement = true;
        }
    }
}
