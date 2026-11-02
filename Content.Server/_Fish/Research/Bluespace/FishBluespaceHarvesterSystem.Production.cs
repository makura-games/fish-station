using Content.Shared._Fish.Research.Bluespace;
using Content.Shared.Database;
using Content.Shared.Physics;
using Robust.Shared.Map;

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

    private bool TryGetOutput(Entity<FishBluespaceHarvesterComponent> ent, out EntityCoordinates coordinates,
        HashSet<EntityUid>? anomalies = null)
    {
        coordinates = default;
        if (!IsOnWorldFloor(ent) || !_transformQuery.TryComp(ent, out var xform) ||
            xform.GridUid is not { } grid || !_gridQuery.TryComp(grid, out var mapGrid) ||
            !_transform.TryGetGridTilePosition((ent.Owner, xform), out var origin, mapGrid))
            return false;

        foreach (var offset in OutputOffsets)
        {
            var candidate = _map.GridTileToLocal(grid, mapGrid, origin + offset);
            if (!IsSafeOutput(ent, candidate) || IsAnomalyOutputOccupied(candidate, anomalies))
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
