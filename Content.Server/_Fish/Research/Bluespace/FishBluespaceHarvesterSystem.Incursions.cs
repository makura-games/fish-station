using Content.Shared.Anomaly.Components;
using Content.Shared.Database;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Spawners;

namespace Content.Server._Fish.Research.Bluespace;

public sealed partial class FishBluespaceHarvesterSystem
{
    // Fish-edit: волны существ и аномалии используют отдельные таймеры и общий запрет вложенного спавна на станции.
    [Dependency] private EntityQuery<MobStateComponent> _mobStateQuery = default!;
    [Dependency] private EntityQuery<AnomalyComponent> _anomalyQuery = default!;

    private const int MaximumActiveIncursions = 6;
    private const int MaximumActiveAnomalies = 2;

    /// <summary>Releases a bounded hostile wave within the shared station cooldown and living creature budget.</summary>
    public bool TryOpenIncursion(Entity<FishBluespaceHarvesterComponent> ent)
    {
        if (!CanOpenIncursion(ent, out var station, out var coordinates))
            return false;

        var incidents = EnsureComp<FishBluespaceHarvesterStationComponent>(station);
        incidents.Incursions.RemoveWhere(uid => !IsActiveIncursion(uid));
        var waveSize = _bluespaceBackground.HasAnomalyRisk(ent) ? ent.Comp.DangerousWaveSize : ent.Comp.IncursionWaveSize;
        var count = Math.Min(Math.Clamp(waveSize, 1, MaximumActiveIncursions),
            Math.Min(ent.Comp.MaxActiveIncursions, MaximumActiveIncursions) - incidents.Incursions.Count);
        incidents.Spawning = true;
        incidents.NextIncident = _timing.CurTime + TimeSpan.FromSeconds(
            Math.Clamp(ent.Comp.IncidentCooldown.TotalSeconds, 40d, 600d));
        try
        {
            var rift = Spawn(ent.Comp.RiftPrototype, coordinates);
            if (!TerminatingOrDeleted(rift))
                EnsureComp<TimedDespawnComponent>(rift).Lifetime = 10f;

            for (var i = 0; i < count; i++)
            {
                // Fish-edit: каждый Spawn может сменить станцию, остановить установку или удалить выходной тайл.
                if (!CanContinueIncident(ent, station) || !_bluespaceBackground.HasIncursionRisk(ent) ||
                    !IsSafeOutput(ent, coordinates))
                    break;

                var creature = Spawn(ent.Comp.HostilePrototype, coordinates);
                if (TerminatingOrDeleted(creature))
                    continue;

                incidents.Incursions.Add(creature);
                EnsureComp<TimedDespawnComponent>(creature).Lifetime = (float) Math.Clamp(
                    ent.Comp.IncursionLifetime.TotalSeconds, 1d, 180d);
                _adminLog.Add(LogType.Action, LogImpact.High,
                    $"{ToPrettyString(ent)} opened a bluespace incursion on {ToPrettyString(station)}, releasing {ToPrettyString(creature)}.");
            }
        }
        finally
        {
            incidents.Spawning = false;
        }

        return true;
    }

    /// <summary>Checks operation, prototypes, safe output and station limits without reserving a wave.</summary>
    public bool CanOpenIncursion(Entity<FishBluespaceHarvesterComponent> ent,
        out EntityUid station, out EntityCoordinates coordinates)
    {
        station = default;
        coordinates = default;
        if (!CanOperate(ent) || !_bluespaceBackground.HasIncursionRisk(ent) ||
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
                if (IsActiveIncursion(creature) && ++count >= Math.Min(ent.Comp.MaxActiveIncursions, MaximumActiveIncursions))
                    return false;
            }
        }

        return TryGetOutput(ent, out coordinates);
    }

    /// <summary>Creates a native anomaly which must be handled normally instead of despawning on a timer.</summary>
    public bool TrySpawnAnomaly(Entity<FishBluespaceHarvesterComponent> ent)
    {
        if (!CanSpawnAnomaly(ent, out var station, out var coordinates))
            return false;

        var incidents = EnsureComp<FishBluespaceHarvesterStationComponent>(station);
        incidents.Anomalies.RemoveWhere(uid => !IsActiveAnomaly(uid));
        incidents.Spawning = true;
        incidents.NextAnomaly = _timing.CurTime + TimeSpan.FromSeconds(
            Math.Clamp(ent.Comp.AnomalyCooldown.TotalSeconds, 120d, 3600d));
        try
        {
            var anomaly = Spawn(ent.Comp.AnomalyPrototype, coordinates);
            if (IsActiveAnomaly(anomaly))
                incidents.Anomalies.Add(anomaly);

            _adminLog.Add(LogType.Anomaly, LogImpact.High,
                $"{ToPrettyString(ent)} created bluespace anomaly {ToPrettyString(anomaly)} on {ToPrettyString(station)}.");
        }
        finally
        {
            incidents.Spawning = false;
        }

        return true;
    }

    /// <summary>Checks the separate station anomaly cooldown and active anomaly budget without changing either.</summary>
    public bool CanSpawnAnomaly(Entity<FishBluespaceHarvesterComponent> ent,
        out EntityUid station, out EntityCoordinates coordinates)
    {
        station = default;
        coordinates = default;
        if (!CanOperate(ent) || !_bluespaceBackground.HasAnomalyRisk(ent) ||
            !_bluespaceBackground.TryGetScope(ent, out station) || ent.Comp.MaxActiveAnomalies <= 0 ||
            !_prototype.TryIndex(ent.Comp.AnomalyPrototype, out var anomaly) || anomaly.Abstract ||
            !anomaly.Components.ContainsKey("Anomaly"))
            return false;

        if (TryComp<FishBluespaceHarvesterStationComponent>(station, out var incidents))
        {
            if (incidents.Spawning || _timing.CurTime < incidents.NextAnomaly)
                return false;

            var count = 0;
            foreach (var active in incidents.Anomalies)
            {
                if (IsActiveAnomaly(active) && ++count >= Math.Min(ent.Comp.MaxActiveAnomalies, MaximumActiveAnomalies))
                    return false;
            }
        }

        return TryGetOutput(ent, out coordinates, incidents?.Anomalies);
    }

    private bool IsAnomalyOutputOccupied(EntityCoordinates coordinates, HashSet<EntityUid>? anomalies)
    {
        if (anomalies == null)
            return false;

        foreach (var anomaly in anomalies)
        {
            if (IsActiveAnomaly(anomaly) && _transformQuery.TryComp(anomaly, out var transform) &&
                _transform.InRange(coordinates, transform.Coordinates, 1f))
                return true;
        }

        return false;
    }

    private bool CanContinueIncident(Entity<FishBluespaceHarvesterComponent> ent, EntityUid station)
    {
        return CanOperate(ent) && _bluespaceBackground.TryGetScope(ent, out var currentStation) && currentStation == station;
    }

    private bool IsActiveIncursion(EntityUid creature)
    {
        return !TerminatingOrDeleted(creature) &&
               (!_mobStateQuery.TryComp(creature, out var mobState) || mobState.CurrentState != MobState.Dead);
    }

    private bool IsActiveAnomaly(EntityUid anomaly)
    {
        return !TerminatingOrDeleted(anomaly) && _anomalyQuery.HasComp(anomaly);
    }
}
