using System.Linq;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.FleshCult.Events;

public sealed partial class VentFleshWormsRule : StationEventSystem<VentFleshWormsRuleComponent>
{
    [Dependency] private StationSystem _stationSystem = default!;

    protected override void Started(EntityUid uid, VentFleshWormsRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        // Fish-start: выбираем только обычную станцию, не карту ЦК.
        var targetStation = _stationSystem.GetStations()
            .Where(station => !IsOnCentcomm(station)) // Fish-edit: исключаем ЦК из выбора станции.
            .FirstOrNull();
        // Fish-end

        if (!TryComp(targetStation, out StationDataComponent? data))
        {
            Logger.Info("TargetStation not have StationDataComponent");
            return;
        }

        var spawnLocations = EntityQuery<VentCritterSpawnLocationComponent, TransformComponent>().ToList();

        var grids = data.Grids.ToHashSet();
        // Fish-start: исключаем точки спавна ЦК и гриды вне выбранной станции.
        spawnLocations.RemoveAll(
            backupSpawnLoc =>
                IsOnCentcomm(backupSpawnLoc.Item1.Owner) // Fish-edit: исключаем точки спавна на ЦК.
                || backupSpawnLoc.Item2.GridUid.HasValue && !grids.Contains(backupSpawnLoc.Item2.GridUid.Value));
        // Fish-end

        RobustRandom.Shuffle(spawnLocations);

        var spawnAmount = RobustRandom.Next(10, 20);
        Sawmill.Info($"Spawning {spawnAmount} of {component.SpawnedPrototypeWorm}");
        foreach (var location in spawnLocations)
        {
            if (spawnAmount-- == 0)
                break;

            var coords = Transform(location.Item1.Owner);
            Spawn(component.SpawnedPrototypeWorm, coords.Coordinates);
        }
    }
}
