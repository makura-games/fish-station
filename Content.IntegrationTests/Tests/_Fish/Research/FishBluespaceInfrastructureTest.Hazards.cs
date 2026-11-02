using System.Linq;
using Content.Server._Fish.Research.Bluespace;
using Content.Server.Power.Components;
using Content.Shared._Fish.Research.Bluespace;
using Content.Shared.AlertLevel;
using Content.Shared.Anomaly.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Spawners;

namespace Content.IntegrationTests.Tests._Fish.Research;

public sealed partial class FishBluespaceInfrastructureTest
{
    /// <summary>Halved power and batch energy preserve the real full-power production cadence.</summary>
    [TestCase(FishBluespacePowerMode.Normal, 60)]
    [TestCase(FishBluespacePowerMode.Overdrive, 30)]
    public async Task HarvesterRetainsFullPowerBatchCadence(FishBluespacePowerMode mode, int seconds)
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var harvester = await SpawnHarvester(context);
            var system = server.System<FishBluespaceHarvesterSystem>();
            await server.WaitPost(() =>
            {
                OpenOutput(context);
                // Fish-edit: проверяем заряд вручную, чтобы автоматическая выдача не скрыла момент готовности.
                harvester.Comp.NextOutputCheck = TimeSpan.MaxValue;
                if (mode == FishBluespacePowerMode.Overdrive)
                {
                    system.TrySetPowerMode(harvester, mode, context.User);
                    system.Update(1f);
                }
                server.EntMan.GetComponent<PowerConsumerComponent>(harvester).NetworkLoad.ReceivingPower = 1_000_000f;
                for (var second = 0; second < seconds - 1; second++)
                    system.Update(1f);
            });
            await server.WaitAssertion(() => Assert.That(system.CanProduceReward(harvester, out _), Is.False));
            var produced = false;
            await server.WaitPost(() =>
            {
                system.Update(1f);
                produced = system.TryProduceReward(harvester);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(produced, Is.True);
                Assert.That(harvester.Comp.StoredEnergy, Is.Zero);
                Assert.That(RewardItems(context), Is.Positive);
            });
        });
    }

    /// <summary>Background cannot replace higher primary codes or bypass special protocol locks.</summary>
    [TestCase("Red", false)]
    [TestCase("Green", true)]
    [TestCase("Sierra", false)]
    [TestCase("DeltaNuke", false)]
    [TestCase("Epsilon", false)]
    public async Task BackgroundPreservesHigherAndLockedAlerts(string initial, bool locked)
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var alertLevel = server.System<AlertLevelSystem>();
            string previous = string.Empty;
            await server.WaitPost(() =>
            {
                var alerts = server.EntMan.AddComponent<AlertLevelComponent>(context.FirstStation);
                alerts.AvailableAlertLevels = ["Green", "Blue", "Red", "Sierra", "DeltaNuke", "Epsilon"];
                alertLevel.SetLevel((context.FirstStation, alerts), initial, playSound: false, announce: false, force: true);
                alerts.IsLevelLocked |= locked;
                previous = alerts.CurrentAlertLevel.Id;
                server.System<FishBluespaceBackgroundSystem>().TryAdjustLevel(context.FirstGrid, 85f);
            });
            await server.WaitAssertion(() =>
            {
                var alerts = server.EntMan.GetComponent<AlertLevelComponent>(context.FirstStation);
                Assert.That(alerts.CurrentAlertLevel.Id, Is.EqualTo(previous));
                Assert.That(alertLevel.IsLevelActive((context.FirstStation, alerts), initial), Is.True);
                Assert.That(alerts.IsLevelLocked, Is.EqualTo(locked));
            });
        });
    }

    /// <summary>Small threshold oscillations must not repeatedly override a manual alert decision.</summary>
    [Test]
    public async Task BlueEscalationRearmsOnlyAfterBackgroundRecovery()
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var background = server.System<FishBluespaceBackgroundSystem>();
            await server.WaitPost(() =>
            {
                var alerts = server.EntMan.AddComponent<AlertLevelComponent>(context.FirstStation);
                alerts.AvailableAlertLevels = ["Green", "Blue"];
                background.TryAdjustLevel(context.FirstGrid, 70f);
                server.System<AlertLevelSystem>().SetLevel((context.FirstStation, alerts), "Green",
                    playSound: false, announce: false, force: true);
                background.TryAdjustLevel(context.FirstGrid, -1f);
                background.TryAdjustLevel(context.FirstGrid, 1f);
            });
            await server.WaitAssertion(() => Assert.That(
                server.EntMan.GetComponent<AlertLevelComponent>(context.FirstStation).CurrentAlertLevel.Id, Is.EqualTo("Green")));
            await server.WaitPost(() =>
            {
                background.TryAdjustLevel(context.FirstGrid, -11f);
                background.TryAdjustLevel(context.FirstGrid, 11f);
            });
            await server.WaitAssertion(() => Assert.That(
                server.EntMan.GetComponent<AlertLevelComponent>(context.FirstStation).CurrentAlertLevel.Id, Is.EqualTo("Blue")));
        });
    }

    /// <summary>Native anomalies have a separate station budget and persist after the machinery is switched off.</summary>
    [Test]
    public async Task HarvesterAnomaliesRespectThresholdCooldownAndLifetime()
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var harvester = await SpawnHarvester(context);
            var system = server.System<FishBluespaceHarvesterSystem>();
            var background = server.System<FishBluespaceBackgroundSystem>();
            var spawned = true;
            var repeated = true;
            await server.WaitPost(() =>
            {
                OpenOutput(context);
                background.TryAdjustLevel(harvester, 84f);
                spawned = system.TrySpawnAnomaly(harvester);
            });
            await server.WaitAssertion(() => Assert.That(spawned, Is.False));
            await server.WaitPost(() =>
            {
                background.TryAdjustLevel(harvester, 1f);
                spawned = system.TrySpawnAnomaly(harvester);
                repeated = system.TrySpawnAnomaly(harvester);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(spawned, Is.True);
                Assert.That(repeated, Is.False);
                var incidents = server.EntMan.GetComponent<FishBluespaceHarvesterStationComponent>(context.FirstStation);
                Assert.That(incidents.NextAnomaly, Is.EqualTo(server.Timing.CurTime + TimeSpan.FromSeconds(120)));
                Assert.That(incidents.Incursions, Is.Empty);
                Assert.That(incidents.Anomalies, Has.Count.EqualTo(1));
                var anomaly = incidents.Anomalies.Single();
                Assert.That(server.EntMan.GetComponent<MetaDataComponent>(anomaly).EntityPrototype!.ID, Is.EqualTo("AnomalyBluespace"));
                Assert.That(server.EntMan.HasComponent<AnomalyComponent>(anomaly), Is.True);
                Assert.That(server.EntMan.HasComponent<TimedDespawnComponent>(anomaly), Is.False);
                Assert.That(server.EntMan.HasComponent<FishBluespaceHarvesterStationComponent>(context.SecondStation), Is.False);
            });
            await server.WaitPost(() =>
            {
                var incidents = server.EntMan.GetComponent<FishBluespaceHarvesterStationComponent>(context.FirstStation);
                incidents.NextAnomaly = server.Timing.CurTime;
                spawned = system.TrySpawnAnomaly(harvester);
            });
            await server.WaitAssertion(() => Assert.That(spawned, Is.False,
                "A second anomaly must not overlap the first when it occupies the only output tile."));
            await server.WaitPost(() =>
            {
                var incidents = server.EntMan.GetComponent<FishBluespaceHarvesterStationComponent>(context.FirstStation);
                server.System<SharedMapSystem>().SetTile(context.FirstGrid,
                    new EntityCoordinates(context.FirstGrid, 0.5f, -1.5f), context.Floor);
                spawned = system.TrySpawnAnomaly(harvester);
                incidents.NextAnomaly = server.Timing.CurTime;
                repeated = system.TrySpawnAnomaly(harvester);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(spawned, Is.True);
                Assert.That(repeated, Is.False);
                Assert.That(server.EntMan.GetComponent<FishBluespaceHarvesterStationComponent>(context.FirstStation).Anomalies,
                    Has.Count.EqualTo(2));
            });
            await server.WaitPost(() =>
            {
                var incidents = server.EntMan.GetComponent<FishBluespaceHarvesterStationComponent>(context.FirstStation);
                server.EntMan.DeleteEntity(incidents.Anomalies.First());
                spawned = system.TrySpawnAnomaly(harvester);
                system.TrySetEnabled(harvester, false, context.User);
                background.TryAdjustLevel(harvester, -85f);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(spawned, Is.True, "Neutralized anomalies release budget for a later spawn.");
                var incidents = server.EntMan.GetComponent<FishBluespaceHarvesterStationComponent>(context.FirstStation);
                Assert.That(incidents.Anomalies, Has.Count.EqualTo(2));
                Assert.That(incidents.Anomalies.All(uid => server.EntMan.HasComponent<AnomalyComponent>(uid)), Is.True);
                Assert.That(system.CanSpawnAnomaly(harvester, out _, out _), Is.False);
            });
        });
    }

    /// <summary>Dead creatures release capacity for the next wave.</summary>
    [Test]
    public async Task IncursionBudgetExcludesDeadCreatures()
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var harvester = await SpawnHarvester(context);
            var system = server.System<FishBluespaceHarvesterSystem>();
            var spawned = false;
            await server.WaitPost(() =>
            {
                OpenOutput(context);
                server.System<FishBluespaceBackgroundSystem>().TryAdjustLevel(harvester, 85f);
                system.TryOpenIncursion(harvester);
                var incidents = server.EntMan.GetComponent<FishBluespaceHarvesterStationComponent>(context.FirstStation);
                incidents.NextIncident = server.Timing.CurTime;
                system.TryOpenIncursion(harvester);
                foreach (var creature in incidents.Incursions.Take(2))
                    server.System<MobStateSystem>().ChangeMobState(creature, MobState.Dead);
                incidents.NextIncident = server.Timing.CurTime;
                spawned = system.TryOpenIncursion(harvester);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(spawned, Is.True);
                Assert.That(server.EntMan.GetComponent<FishBluespaceHarvesterStationComponent>(context.FirstStation).Incursions,
                    Has.Count.EqualTo(6));
            });
        });
    }

    /// <summary>Real entity initialization callbacks cannot recursively spend either station hazard budget.</summary>
    [Test]
    public async Task SpawnCallbacksCannotBypassSharedStationLimits()
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var harvester = await SpawnHarvester(context);
            var system = server.System<FishBluespaceHarvesterSystem>();
            var nested = false;
            var callbacks = 0;
            var spawned = false;
            await server.WaitPost(() =>
            {
                OpenOutput(context);
                server.System<FishBluespaceBackgroundSystem>().TryAdjustLevel(harvester, 85f);
                void OnInitialized(Entity<MetaDataComponent> ent)
                {
                    if (ent.Comp.EntityPrototype?.ID is not ("FishBluespaceRift" or "AnomalyBluespace"))
                        return;

                    callbacks++;
                    nested |= system.TryOpenIncursion(harvester) | system.TrySpawnAnomaly(harvester);
                }

                server.EntMan.EntityInitialized += OnInitialized;
                try
                {
                    spawned = system.TryOpenIncursion(harvester) && system.TrySpawnAnomaly(harvester);
                }
                finally
                {
                    server.EntMan.EntityInitialized -= OnInitialized;
                }
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(spawned, Is.True);
                Assert.That(callbacks, Is.EqualTo(2));
                Assert.That(nested, Is.False);
                var incidents = server.EntMan.GetComponent<FishBluespaceHarvesterStationComponent>(context.FirstStation);
                Assert.That(incidents.Incursions, Has.Count.EqualTo(3));
                Assert.That(incidents.Anomalies, Has.Count.EqualTo(1));
                Assert.That(incidents.Spawning, Is.False);
            });
        });
    }
}
