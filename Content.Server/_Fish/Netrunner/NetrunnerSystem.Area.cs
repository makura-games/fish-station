using Content.Shared._Fish.Netrunner;
using Content.Shared.Doors.Components;
using Content.Shared.Light.Components;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Netrunner;

public sealed partial class NetrunnerSystem
{
    // Fish-edit: Массовый скрипт выбирает точку и оплачивается один раз за всю область.
    [Dependency] private EntityLookupSystem _lookup = default!;

    /// <summary>Starts a blackout at a validated location with a bounded set of lights.</summary>
    public bool TryStartAreaHack(Entity<NetrunnerImplantComponent> ent, EntityUid user,
        EntityCoordinates coordinates, ProtoId<NetrunnerAbilityPrototype> id)
    {
        if (!CanStartAreaHack(ent, user, coordinates, id, out var ability))
        {
            if (IsOperator(ent, user))
                _popup.PopupEntity(Loc.GetString("fish-netrunner-area-unavailable"), user, user);
            return false;
        }
        var targets = new List<EntityUid>();
        foreach (var light in _lookup.GetEntitiesInRange<PoweredLightComponent>(coordinates, ability.AreaRadius,
                     LookupFlags.StaticSundries | LookupFlags.Approximate))
        {
            if (CanAffectAreaLight(ent, light, coordinates, ability))
                targets.Add(light);
        }
        if (targets.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("fish-netrunner-area-empty"), user, user);
            return false;
        }
        return StartProgram(ent, user, targets[0], targets[0], id, ability, coordinates, targets);
    }

    /// <summary>Checks ownership, resources and visibility of the selected area center.</summary>
    public bool CanStartAreaHack(Entity<NetrunnerImplantComponent> ent, EntityUid user,
        EntityCoordinates coordinates, ProtoId<NetrunnerAbilityPrototype> id, out NetrunnerAbilityPrototype ability)
        => CanRunProgram(ent, user, id, out ability) && ability.Kind == NetrunnerHackKind.DisableLight
            && ability.AreaRadius > 0 && CanTargetArea(ent, coordinates, ability);

    private bool CanTargetArea(Entity<NetrunnerImplantComponent> ent, EntityCoordinates coordinates,
        NetrunnerAbilityPrototype ability)
    {
        if (!_transform.IsValid(coordinates) || ent.Comp.User is not { } user
            || ent.Comp.Chair != null && !CanUseChair(ent))
            return false;
        var origin = ent.Comp.RemoteEye ?? user;
        if (!_transform.InRange(Transform(origin).Coordinates, coordinates, ability.Range))
            return false;
        if (ent.Comp.RemoteEye is not { } remote)
            return _interaction.InRangeUnobstructed(origin, coordinates, ability.Range, CollisionGroup.Opaque);
        return TryComp<NetrunnerRemoteEyeComponent>(remote, out var viewer)
            && IsAreaCellVisible(coordinates, viewer.Station);
    }

    private bool IsAreaCellVisible(EntityCoordinates coordinates, EntityUid station)
    {
        var gridUid = _transform.GetGrid(coordinates);
        if (gridUid is not { } grid || _station.GetOwningStation(grid) != station
            || !TryComp<MapGridComponent>(grid, out var mapGrid)
            || !TryComp<BroadphaseComponent>(grid, out var broadphase))
            return false;
        var local = _transform.WithEntityId(coordinates, grid);
        return _vision.IsNetrunnerAccessible((grid, broadphase, mapGrid),
            _maps.LocalToTile(grid, mapGrid, local), station);
    }

    private bool CanAffectAreaLight(Entity<NetrunnerImplantComponent> ent, EntityUid light,
        EntityCoordinates center, NetrunnerAbilityPrototype ability)
    {
        if (Terminating(light) || Paused(light) || _container.IsEntityInContainer(light)
            || !_transform.InRange(Transform(light).Coordinates, center, ability.AreaRadius)
            || Transform(light).GridUid != _transform.GetGrid(center))
            return false;
        if (ent.Comp.RemoteEye is { } remote && TryComp<NetrunnerRemoteEyeComponent>(remote, out var viewer)
            && !IsAreaCellVisible(Transform(light).Coordinates, viewer.Station))
            return false;
        var check = new NetrunnerValidateHackEvent(ent.Comp.User!.Value, ability);
        RaiseLocalEvent(light, ref check);
        return check.Supported && check.Allowed && check.Device == light;
    }

    private bool ApplyAreaHack(Entity<NetrunnerImplantComponent> ent, NetrunnerPendingHack pending,
        NetrunnerAbilityPrototype ability)
    {
        if (pending.AreaCoordinates is not { } center || pending.AreaTargets is not { } targets
            || ent.Comp.User is not { } user)
            return false;
        for (var i = targets.Count - 1; i >= 0; i--)
        {
            var target = targets[i];
            if (!CanAffectAreaLight(ent, target, center, ability))
            {
                targets.RemoveAt(i);
                continue;
            }
            var apply = new NetrunnerApplyHackEvent(user, target, ability);
            RaiseLocalEvent(target, ref apply);
            if (!apply.Applied)
                targets.RemoveAt(i);
        }
        return targets.Count > 0;
    }

    private void RecordDeviceAudits(NetrunnerPendingHack pending, NetrunnerLogEntry entry)
    {
        if (pending.AreaTargets is { } targets)
        {
            foreach (var target in targets)
                RecordDeviceAudit(target, entry);
        }
        else
            RecordDeviceAudit(entry.Kind == NetrunnerHackKind.BatteryDrain ? pending.Target : pending.Device, entry);
    }

    private void RecordDeviceAudit(EntityUid device, NetrunnerLogEntry entry)
    {
        if (Terminating(device))
            return;
        var audit = EnsureComp<NetrunnerAuditComponent>(device);
        if (HasComp<AirlockComponent>(device))
            audit.Retention = TimeSpan.FromMinutes(10);
        AppendJournal(audit.Entries, entry);
    }
}
