using Content.Shared._Fish.Research.Bluespace;
using Content.Shared.Database;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Spawners;

namespace Content.Server._Fish.Research.Bluespace;

public sealed partial class FishBluespaceHarvesterSystem
{
    // Fish-edit: партия расходует заряд до первого Spawn, а инциденты резервируют общий бюджет станции.
    /// <summary>Claims one completed reward before spawning a bounded, weighted batch on safe floor.</summary>
    public bool TryProduceReward(Entity<FishBluespaceHarvesterComponent> ent)
    {
        if (!CanProduceReward(ent, out var coordinates))
        {
            if (!ent.Comp.Producing && CanOperate(ent) && ent.Comp.StoredEnergy >= ent.Comp.EnergyPerReward)
                ent.Comp.OutputBlocked = true;
            return false;
        }

        var reward = PickLoot(ent.Comp.LootCategory);
        if (reward == null)
            return false;

        ent.Comp.Producing = true;
        ent.Comp.StoredEnergy = 0d;
        ent.Comp.OutputBlocked = false;
        try
        {
            _adminLog.Add(LogType.Action, LogImpact.Low,
                $"{ToPrettyString(ent)} claimed bluespace harvester reward {reward.ID} at {coordinates}.");
            var spawnedEntities = 0;
            foreach (var (product, count) in reward.Products)
            {
                var remaining = count;
                while (remaining > 0)
                {
                    // Fish-edit: каждый Spawn может вызвать вложенные события и изменить допустимость выхода.
                    if (spawnedEntities >= MaximumBatchSize || !CanOperate(ent) || !IsSafeOutput(ent, coordinates))
                        return true;

                    var maxCount = _stack.GetMaxCount(product);
                    if (maxCount <= 0)
                        return true;

                    // Fish-edit: количество задаётся в единицах, а каждый вызов создаёт одну штатную стопку.
                    var units = Math.Min(remaining, maxCount);
                    remaining -= units;
                    spawnedEntities++;
                    _stack.SpawnMultipleAtPosition(product, units, coordinates);
                }
            }
        }
        finally
        {
            ent.Comp.Producing = false;
        }

        return true;
    }

    /// <summary>Checks real operation, a complete charge, a valid reward table and safe floor outside the footprint.</summary>
    public bool CanProduceReward(Entity<FishBluespaceHarvesterComponent> ent, out EntityCoordinates coordinates)
    {
        coordinates = default;
        return !ent.Comp.Producing && CanOperate(ent) && double.IsFinite(ent.Comp.StoredEnergy) &&
               ent.Comp.StoredEnergy >= ent.Comp.EnergyPerReward &&
               GetTotalLootWeight(ent.Comp.LootCategory) > 0d && TryGetOutput(ent, out coordinates);
    }

    /// <summary>Opens at most one short-lived local rift within the shared station cooldown and creature budget.</summary>
    public bool TryOpenIncursion(Entity<FishBluespaceHarvesterComponent> ent)
    {
        if (!CanOpenIncursion(ent, out var station, out var coordinates))
            return false;

        var incidents = EnsureComp<FishBluespaceHarvesterStationComponent>(station);
        incidents.Incursions.RemoveWhere(uid => TerminatingOrDeleted(uid));
        incidents.Spawning = true;
        incidents.NextIncident = _timing.CurTime + TimeSpan.FromSeconds(
            Math.Clamp(ent.Comp.IncidentCooldown.TotalSeconds, 60d, 600d));
        try
        {
            var rift = Spawn(ent.Comp.RiftPrototype, coordinates);
            EnsureComp<TimedDespawnComponent>(rift).Lifetime = 10f;
            _adminLog.Add(LogType.Action, LogImpact.High,
                $"{ToPrettyString(ent)} opened {ToPrettyString(rift)} on {ToPrettyString(station)}.");
            if (!CanOperate(ent) || !IsSafeOutput(ent, coordinates))
                return true;

            var creature = Spawn(ent.Comp.HostilePrototype, coordinates);
            incidents.Incursions.Add(creature);
            EnsureComp<TimedDespawnComponent>(creature).Lifetime = (float) Math.Clamp(
                ent.Comp.IncursionLifetime.TotalSeconds, 1d, 180d);
            _adminLog.Add(LogType.Action, LogImpact.High,
                $"{ToPrettyString(ent)} opened a local bluespace incursion on {ToPrettyString(station)}, releasing {ToPrettyString(creature)}.");
        }
        finally
        {
            incidents.Spawning = false;
        }

        return true;
    }

    /// <summary>Checks local floor, configured prototypes and station-wide limits without reserving a spawn.</summary>
    public bool CanOpenIncursion(Entity<FishBluespaceHarvesterComponent> ent,
        out EntityUid station, out EntityCoordinates coordinates)
    {
        station = default;
        coordinates = default;
        if (!CanOperate(ent) || !float.IsFinite(ent.Comp.IncidentLevel) ||
            _bluespaceBackground.GetLevel(ent) < Math.Clamp(ent.Comp.IncidentLevel, 0f, 100f) ||
            !_bluespaceBackground.TryGetScope(ent, out station) ||
            !_prototype.TryIndex(ent.Comp.RiftPrototype, out var rift) || rift.Abstract ||
            !_prototype.TryIndex(ent.Comp.HostilePrototype, out var hostile) || hostile.Abstract ||
            ent.Comp.MaxActiveIncursions <= 0)
            return false;

        if (TryComp<FishBluespaceHarvesterStationComponent>(station, out var incidents))
        {
            if (incidents.Spawning || _timing.CurTime < incidents.NextIncident)
                return false;

            var count = 0;
            foreach (var creature in incidents.Incursions)
            {
                if (!TerminatingOrDeleted(creature) && ++count >= Math.Min(ent.Comp.MaxActiveIncursions, 2))
                    return false;
            }
        }

        return TryGetOutput(ent, out coordinates);
    }

    private double GetTotalLootWeight(FishBluespaceLootCategory category)
    {
        var total = 0d;
        foreach (var reward in _prototype.EnumeratePrototypes<FishBluespaceHarvesterLootPrototype>())
        {
            if (reward.Category == category && IsValidLoot(reward))
                total += reward.Weight;
        }

        return double.IsFinite(total) ? total : 0d;
    }

    private FishBluespaceHarvesterLootPrototype? PickLoot(FishBluespaceLootCategory category)
    {
        var total = GetTotalLootWeight(category);
        if (total <= 0d)
            return null;

        var roll = _random.NextDouble() * total;
        FishBluespaceHarvesterLootPrototype? last = null;
        foreach (var reward in _prototype.EnumeratePrototypes<FishBluespaceHarvesterLootPrototype>())
        {
            if (reward.Category != category || !IsValidLoot(reward))
                continue;

            last = reward;
            roll -= reward.Weight;
            if (roll < 0d)
                return reward;
        }

        return last;
    }

    private bool IsValidLoot(FishBluespaceHarvesterLootPrototype reward)
    {
        if (!float.IsFinite(reward.Weight) || reward.Weight <= 0f || reward.Products.Count == 0 ||
            reward.Products.Count > MaximumBatchSize)
            return false;

        var totalUnits = 0;
        var totalEntities = 0;
        foreach (var (product, count) in reward.Products)
        {
            if (count <= 0 || count > MaximumBatchUnits ||
                !_prototype.TryIndex(product, out var prototype) || prototype.Abstract)
                return false;

            var maxCount = _stack.GetMaxCount(prototype);
            if (maxCount <= 0)
                return false;

            totalUnits += count;
            totalEntities += 1 + (count - 1) / maxCount;
            if (totalUnits > MaximumBatchUnits || totalEntities > MaximumBatchSize)
                return false;
        }

        return true;
    }

    private bool TryGetOutput(Entity<FishBluespaceHarvesterComponent> ent, out EntityCoordinates coordinates)
    {
        coordinates = default;
        if (!IsOnWorldFloor(ent) || !_transformQuery.TryComp(ent, out var xform) ||
            xform.GridUid is not { } grid || !_gridQuery.TryComp(grid, out var mapGrid) ||
            !_transform.TryGetGridTilePosition((ent.Owner, xform), out var origin, mapGrid))
            return false;

        foreach (var offset in OutputOffsets)
        {
            var candidate = _map.GridTileToLocal(grid, mapGrid, origin + offset);
            if (!IsSafeOutput(ent, candidate))
                continue;

            coordinates = candidate;
            return true;
        }

        return false;
    }

    private bool IsSafeOutput(Entity<FishBluespaceHarvesterComponent> ent, EntityCoordinates coordinates)
    {
        if (!_transformQuery.TryComp(ent, out var xform) || xform.GridUid != coordinates.EntityId ||
            !_bluespaceBackground.TryGetScope(ent, out var station) ||
            !_bluespaceBackground.TryGetScope(coordinates.EntityId, out var outputStation) || outputStation != station ||
            _turf.GetTileRef(coordinates) is not { } tile || tile.GridUid != coordinates.EntityId ||
            _turf.IsSpace(tile) || _turf.IsTileBlocked(tile, CollisionGroup.FullTileMask, 0f))
            return false;

        return _interaction.InRangeUnobstructed(ent.Owner, _transform.ToMapCoordinates(coordinates), 3.5f);
    }
}
