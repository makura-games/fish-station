using Content.Server.Fluids.EntitySystems;
using Content.Shared._Fish.Mechs;
using Content.Shared._Fish.Mechs.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Maps;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Mechs;

/// <summary>
/// Серверные тики internal damage и thrusters.
/// </summary>
public sealed class MechServerSystem : EntitySystem
{
    [Dependency] private readonly SharedMechSystem _mech = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _nextTick;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextTick)
            return;

        _nextTick = _timing.CurTime + TimeSpan.FromSeconds(1);

        var internalQuery = EntityQueryEnumerator<MechInternalDamageComponent, MechComponent>();
        while (internalQuery.MoveNext(out var uid, out var internalDamage, out var mech))
        {
            if ((internalDamage.Damage & MechInternalDamageFlags.PowerSpike) != 0)
                _mech.TryChangeEnergy(uid, -internalDamage.PowerSpikeDrainPerSecond, mech);

            if ((internalDamage.Damage & MechInternalDamageFlags.CabinFire) != 0)
            {
                var fire = new DamageSpecifier();
                fire.DamageDict.Add("Heat", internalDamage.CabinFireDamagePerSecond);
                _damageable.TryChangeDamage(uid, fire);
            }
        }

        var thrusterQuery = EntityQueryEnumerator<MechThrustersComponent, MechComponent>();
        while (thrusterQuery.MoveNext(out var uid, out var thrusters, out var mech))
        {
            if (!thrusters.Active)
                continue;

            if (!_mech.TryChangeEnergy(uid, -thrusters.EnergyPerSecond, mech))
            {
                thrusters.Active = false;
                Dirty(uid, thrusters);
            }
        }
    }
}

/// <summary>
/// Server override для спавна дыма.
/// </summary>
public sealed class MechChassisAbilitySystem : SharedMechChassisAbilitySystem
{
    [Dependency] private readonly SmokeSystem _smoke = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IMapManager _mapMan = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    protected override bool LaunchSmokeEffect(Entity<MechSmokeComponent> ent)
    {
        var xform = Transform(ent);
        var mapCoords = _xform.GetMapCoordinates(ent, xform);
        if (!_mapMan.TryFindGridAt(mapCoords, out var gridUid, out var gridComp) ||
            !_map.TryGetTileRef(gridUid, gridComp, xform.Coordinates, out var tileRef) ||
            tileRef.Tile.IsEmpty ||
            _turf.IsSpace(tileRef))
        {
            Popup.PopupEntity(Loc.GetString("mech-smoke-failed"), ent);
            return false;
        }

        var coords = _map.MapToGrid(gridUid, mapCoords);
        var smoke = Spawn(ent.Comp.SmokePrototype, coords.SnapToGrid());
        if (!TryComp(smoke, out SmokeComponent? smokeComp))
        {
            QueueDel(smoke);
            return false;
        }

        _smoke.StartSmoke(smoke, new Solution(), ent.Comp.DurationSeconds, ent.Comp.SpreadAmount, smokeComp);
        return true;
    }
}
