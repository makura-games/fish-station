using Content.Shared._Fish.BattleShuttles.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Client._Fish.BattleShuttles;

/// <summary>
/// Визуальный выхлоп при движении Battle Shuttle (переиспользует JetpackEffect).
/// </summary>
public sealed class BattleShuttleEngineTrailSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;

    public override void Update(float frameTime)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<BattleShuttleEngineTrailComponent, TransformComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var trail, out var xform, out var body))
        {
            if (body.LinearVelocity.LengthSquared() < trail.MinSpeedSquared)
                continue;

            if (_transform.InRange(xform.Coordinates, trail.LastCoordinates, 0.6f) &&
                _timing.CurTime < trail.TargetTime)
                continue;

            trail.LastCoordinates = _transform.GetMoverCoordinates(xform.Coordinates);
            trail.TargetTime = _timing.CurTime + TimeSpan.FromSeconds(trail.EffectCooldown);

            SpawnTrail(uid, trail, xform);
        }
    }

    private void SpawnTrail(EntityUid uid, BattleShuttleEngineTrailComponent trail, TransformComponent xform)
    {
        var coordinates = xform.Coordinates;
        var gridUid = _transform.GetGrid(coordinates);

        if (TryComp<MapGridComponent>(gridUid, out var grid))
        {
            coordinates = new EntityCoordinates(gridUid.Value,
                _mapSystem.WorldToLocal(gridUid.Value, grid, _transform.ToMapCoordinates(coordinates).Position));
        }
        else if (xform.MapUid != null)
        {
            coordinates = new EntityCoordinates(xform.MapUid.Value, _transform.GetWorldPosition(xform));
        }
        else
        {
            return;
        }

        Spawn(trail.EffectPrototype, coordinates);
    }
}
