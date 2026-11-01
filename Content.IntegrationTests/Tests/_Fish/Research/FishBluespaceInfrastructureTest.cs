using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Server._Fish.Research.Bluespace;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Station.Systems;
using Content.Server.Stack;
using Content.Shared._Fish.Research.Bluespace;
using Content.Shared.Power.Components;
using Content.Shared.Research.Components;
using Content.Shared.Station.Components;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using ServerResearchSystem = Content.Server.Research.Systems.ResearchSystem;

namespace Content.IntegrationTests.Tests._Fish.Research;

/// <summary>Exercises station ownership, controlled native power samples and bounded infrastructure output.</summary>
[TestFixture, NonParallelizable]
public sealed class FishBluespaceInfrastructureTest
{
    /// <summary>Independent station scopes clamp finite adjustments and retain the critical safety latch until recovery.</summary>
    [Test]
    public async Task BackgroundIsStationScopedFiniteAndUsesRecoveryHysteresis()
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var background = server.System<FishBluespaceBackgroundSystem>();
            await server.WaitAssertion(() =>
            {
                Assert.That(background.TryGetScope(context.FirstGrid, out var first), Is.True);
                Assert.That(first, Is.EqualTo(context.FirstStation));
                Assert.That(background.TryGetScope(context.SecondGrid, out var second), Is.True);
                Assert.That(second, Is.EqualTo(context.SecondStation));
                Assert.That(background.GetLevel(context.FirstGrid), Is.Zero);
                Assert.That(server.EntMan.HasComponent<FishBluespaceBackgroundComponent>(context.FirstStation), Is.False,
                    "Reading background must not create mutable station state.");
            });
            var adjusted = false;
            var nonfinite = true;
            await server.WaitPost(() =>
            {
                adjusted = background.TryAdjustLevel(context.FirstGrid, float.MaxValue);
                nonfinite = background.TryAdjustLevel(context.FirstGrid, float.NaN) |
                            background.TryAdjustLevel(context.FirstGrid, float.PositiveInfinity);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(adjusted, Is.True);
                Assert.That(nonfinite, Is.False);
                Assert.That(background.GetLevel(context.FirstGrid), Is.EqualTo(100f));
                Assert.That(background.GetLevel(context.SecondGrid), Is.Zero);
                Assert.That(background.IsCritical(context.FirstGrid), Is.True);
            });
            await server.WaitPost(() => background.TryAdjustLevel(context.FirstGrid, -20f));
            await server.WaitAssertion(() => Assert.That(background.IsCritical(context.FirstGrid), Is.True,
                "Exactly eighty background must retain the critical latch."));
            await server.WaitPost(() => background.TryAdjustLevel(context.FirstGrid, -0.1f));
            await server.WaitAssertion(() => Assert.That(background.IsCritical(context.FirstGrid), Is.False));
            await server.WaitPost(() => background.TryAdjustLevel(context.FirstGrid, -float.MaxValue));
            await server.WaitAssertion(() => Assert.That(background.GetLevel(context.FirstGrid), Is.Zero));
        });
    }

    /// <summary>A prober needs actual working power and a live same-station server, with no paused or offline accrual.</summary>
    [Test]
    public async Task ProberCreditsOnlyPoweredSameStationResearchAndNeverCatchesUp()
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var entMan = server.EntMan;
            var research = server.System<ServerResearchSystem>();
            var devices = server.System<FishBluespaceResearchSystem>();
            var background = server.System<FishBluespaceBackgroundSystem>();
            EntityUid firstServer = default;
            EntityUid secondServer = default;
            Entity<FishBluespaceProberComponent> prober = default;
            var enabled = false;
            var firstBefore = 0;
            var secondBefore = 0;
            await server.WaitPost(() =>
            {
                firstServer = entMan.SpawnEntity("ResearchAndDevelopmentServer",
                    new EntityCoordinates(context.FirstGrid, -0.5f, -0.5f));
                secondServer = entMan.SpawnEntity("ResearchAndDevelopmentServer", context.SecondCoordinates);
                server.System<PowerReceiverSystem>().SetNeedsPower(firstServer, false);
                server.System<PowerReceiverSystem>().SetNeedsPower(secondServer, false);
                // Fish-edit: в управляемой фикстуре серверы доступны сразу, без отдельного расчёта APC-сети.
                entMan.GetComponent<ApcPowerReceiverComponent>(firstServer).Powered = true;
                entMan.GetComponent<ApcPowerReceiverComponent>(secondServer).Powered = true;
                firstBefore = entMan.GetComponent<ResearchServerComponent>(firstServer).Points;
                secondBefore = entMan.GetComponent<ResearchServerComponent>(secondServer).Points;
                var uid = entMan.SpawnEntity("FishBluespaceProber", context.FirstCoordinates);
                prober = (uid, entMan.GetComponent<FishBluespaceProberComponent>(uid));
                research.RegisterClient(prober, firstServer);
                SetApcReceipt(context, prober, 0.01f);
                server.System<SharedUserInterfaceSystem>().TryOpenUi(prober.Owner, FishBluespaceDeviceUiKey.Key, context.User);
                enabled = devices.TrySetEnabled(prober, context.User, true);
                devices.Update(1f);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(enabled, Is.True);
                Assert.That(entMan.GetComponent<ResearchServerComponent>(firstServer).Points, Is.EqualTo(firstBefore));
                Assert.That(entMan.GetComponent<ResearchServerComponent>(secondServer).Points, Is.EqualTo(secondBefore));
                Assert.That(background.GetLevel(prober), Is.Zero,
                    "Idle-level receipt must not count as working power even when Powered is true.");
            });
            await server.WaitPost(() =>
            {
                SetApcReceipt(context, prober, 1f);
                entMan.GetComponent<ApcPowerReceiverComponent>(prober).Powered = false;
                devices.Update(1f);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(entMan.GetComponent<ResearchServerComponent>(firstServer).Points, Is.EqualTo(firstBefore));
                Assert.That(background.GetLevel(prober), Is.Zero,
                    "A stale receipt must not bypass the native receiver's unpowered state.");
            });
            await server.WaitPost(() =>
            {
                SetApcReceipt(context, prober, 1f);
                devices.Update(1f);
            });
            var earned = 0;
            var level = 0f;
            await server.WaitAssertion(() =>
            {
                earned = entMan.GetComponent<ResearchServerComponent>(firstServer).Points - firstBefore;
                level = background.GetLevel(prober);
                Assert.That(earned, Is.GreaterThan(0));
                Assert.That(level, Is.GreaterThan(0f));
                Assert.That(entMan.GetComponent<ResearchServerComponent>(secondServer).Points, Is.EqualTo(secondBefore));
                Assert.That(background.GetLevel(context.SecondGrid), Is.Zero);
            });
            await server.WaitPost(() =>
            {
                research.UnregisterClient(prober);
                research.RegisterClient(prober, secondServer);
                SetApcReceipt(context, prober, 1f);
                devices.Update(300f);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(entMan.GetComponent<ResearchServerComponent>(firstServer).Points, Is.EqualTo(firstBefore + earned));
                Assert.That(entMan.GetComponent<ResearchServerComponent>(secondServer).Points, Is.EqualTo(secondBefore),
                    "A powered prober must not credit an R&D server on another station.");
                Assert.That(background.GetLevel(prober), Is.EqualTo(level));
            });
            await server.WaitPost(() =>
            {
                research.UnregisterClient(prober);
                research.RegisterClient(prober, firstServer);
                server.System<MetaDataSystem>().SetEntityPaused(prober, true);
                devices.Update(300f);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(entMan.GetComponent<ResearchServerComponent>(firstServer).Points, Is.EqualTo(firstBefore + earned));
                Assert.That(background.GetLevel(prober), Is.EqualTo(level));
            });
            await server.WaitPost(() =>
            {
                server.System<MetaDataSystem>().SetEntityPaused(prober, false);
                SetApcReceipt(context, prober, 1f);
                devices.Update(300f);
            });
            await server.WaitAssertion(() => Assert.That(entMan.GetComponent<ResearchServerComponent>(firstServer).Points,
                Is.InRange(firstBefore + earned + 1, firstBefore + earned * 3),
                "A long resumed frame may credit at most one bounded second, without offline catch-up."));
            var afterResume = 0;
            await server.WaitPost(() =>
            {
                afterResume = entMan.GetComponent<ResearchServerComponent>(firstServer).Points;
                entMan.DeleteEntity(firstServer);
                SetApcReceipt(context, prober, 1f);
                devices.Update(300f);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(afterResume, Is.GreaterThan(firstBefore));
                Assert.That(entMan.GetComponent<ResearchServerComponent>(secondServer).Points, Is.EqualTo(secondBefore));
                Assert.That(background.GetLevel(prober), Is.LessThanOrEqualTo(level * 2.1f),
                    "Deleting the linked server must stop background generation as well as research credit.");
            });
        });
    }

    /// <summary>Suppression needs real working receipt and continues to lower only its own critically unstable station.</summary>
    [Test]
    public async Task SuppressorRequiresWorkingPowerAndRemainsUsefulDuringCriticalShutdown()
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var background = server.System<FishBluespaceBackgroundSystem>();
            var devices = server.System<FishBluespaceResearchSystem>();
            Entity<FishBluespaceSuppressorComponent> suppressor = default;
            var enabled = false;
            await server.WaitPost(() =>
            {
                var uid = server.EntMan.SpawnEntity("FishBluespaceSuppressor", context.FirstCoordinates);
                suppressor = (uid, server.EntMan.GetComponent<FishBluespaceSuppressorComponent>(uid));
                background.TryAdjustLevel(suppressor, 95f);
                background.TryAdjustLevel(context.SecondGrid, 40f);
                SetApcReceipt(context, suppressor, 0.01f);
                server.System<SharedUserInterfaceSystem>().TryOpenUi(uid, FishBluespaceDeviceUiKey.Key, context.User);
                enabled = devices.TrySetEnabled(suppressor, context.User, true);
                devices.Update(1f);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(enabled, Is.True);
                Assert.That(background.IsCritical(suppressor), Is.True);
                Assert.That(background.GetLevel(suppressor), Is.EqualTo(95f));
            });
            await server.WaitPost(() =>
            {
                SetApcReceipt(context, suppressor, 1f);
                devices.Update(300f);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(background.GetLevel(suppressor), Is.InRange(94.8f, 94.99f),
                    "Suppression must run through the safety latch and clamp delayed frames to one second.");
                Assert.That(background.GetLevel(context.SecondGrid), Is.EqualTo(40f));
            });
        });
    }

    /// <summary>Energy follows actual received watts, with normal and overdrive load caps and no stale power sample.</summary>
    [Test]
    public async Task HarvesterAccumulatesActualEnergyAtItsSelectedLoad()
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var harvester = await SpawnHarvester(context);
            var system = server.System<FishBluespaceHarvesterSystem>();
            var consumer = server.EntMan.GetComponent<PowerConsumerComponent>(harvester);
            var switched = false;
            await server.WaitPost(() =>
            {
                consumer.NetworkLoad.ReceivingPower = 125_000f;
                system.Update(1f);
            });
            await server.WaitAssertion(() => Assert.That(harvester.Comp.StoredEnergy, Is.EqualTo(125_000d).Within(1d)));
            await server.WaitPost(() =>
            {
                consumer.NetworkLoad.ReceivingPower = 3_000_000f;
                system.Update(1f);
                switched = system.TrySetPowerMode(harvester, FishBluespacePowerMode.Overdrive, context.User);
                system.Update(1f);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(switched, Is.True);
                Assert.That(harvester.Comp.StoredEnergy, Is.EqualTo(375_000d).Within(1d),
                    "Changing demand must discard a stale sample from the previous power mode.");
            });
            await server.WaitPost(() =>
            {
                consumer.NetworkLoad.ReceivingPower = 3_000_000f;
                system.Update(300f);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(harvester.Comp.StoredEnergy, Is.EqualTo(875_000d).Within(1d),
                    "Overdrive caps received power at five hundred kilowatts and a delayed frame at one second.");
                Assert.That(consumer.DrawRate, Is.EqualTo(500_000f));
                Assert.That(server.System<FishBluespaceBackgroundSystem>().GetLevel(harvester), Is.EqualTo(0.825f).Within(0.001f),
                    "Lower power must preserve the previous background generation per minute.");
                Assert.That(server.System<FishBluespaceBackgroundSystem>().GetLevel(context.SecondGrid), Is.Zero);
            });
        });
    }

    /// <summary>A missing safe output retains one charged batch; reopening one floor tile permits exactly one claim.</summary>
    [Test]
    public async Task HarvesterCapsBlockedProgressAndCannotClaimTheSameRewardTwice()
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var harvester = await SpawnHarvester(context);
            var system = server.System<FishBluespaceHarvesterSystem>();
            var produced = true;
            await server.WaitPost(() =>
            {
                server.EntMan.GetComponent<PowerConsumerComponent>(harvester).NetworkLoad.ReceivingPower = 1_000_000f;
                for (var second = 0; second < 70; second++)
                    system.Update(1f);
                produced = system.TryProduceReward(harvester);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(produced, Is.False);
                Assert.That(harvester.Comp.OutputBlocked, Is.True);
                Assert.That(harvester.Comp.StoredEnergy, Is.EqualTo(15_000_000d));
                Assert.That(RewardItems(context), Is.Zero);
            });
            var repeated = true;
            await server.WaitPost(() =>
            {
                OpenOutput(context);
                produced = system.TryProduceReward(harvester);
                repeated = system.TryProduceReward(harvester);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(produced, Is.True);
                Assert.That(repeated, Is.False);
                Assert.That(harvester.Comp.StoredEnergy, Is.Zero);
                Assert.That(harvester.Comp.OutputBlocked, Is.False);
                Assert.That(RewardItems(context), Is.InRange(1, 16));
                Assert.That(RewardUnits(context), Is.InRange(16, 30));
                Assert.That(GetRewardEntities(context).All(uid => !server.EntMan.TryGetComponent<StackComponent>(uid, out var stack) ||
                    stack.Count <= server.System<StackSystem>().GetMaxCount(stack)), Is.True);
            });
        });
    }

    /// <summary>Configured quantities describe units, split at native stack limits and reject excessive physical or unit totals.</summary>
    [TestCase("SheetSteel1", 61, 3, true, false)]
    [TestCase("SheetSteel1", 300, 10, true, false)]
    [TestCase("SheetSteel1", 301, 0, false, false)]
    [TestCase("SheetSteel1", 301, 0, false, true)]
    [TestCase("Beaker", 16, 16, true, false)]
    [TestCase("Beaker", 17, 0, false, false)]
    [TestCase("SheetSteel1", 0, 0, false, false)]
    public async Task HarvesterSplitsUnitsAndEnforcesIndependentOutputCaps(string product, int units, int entities,
        bool accepted, bool mixedMaterials)
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var harvester = await SpawnHarvester(context);
            var system = server.System<FishBluespaceHarvesterSystem>();
            var originals = new Dictionary<FishBluespaceHarvesterLootPrototype, Dictionary<EntProtoId, int>>();
            var produced = false;
            var repeated = true;
            try
            {
                await server.WaitPost(() =>
                {
                    // Fish-edit: все варианты одной категории получают одинаковую тестовую партию, исключая влияние RNG.
                    foreach (var reward in server.ProtoMan.EnumeratePrototypes<FishBluespaceHarvesterLootPrototype>())
                    {
                        if (reward.Category != FishBluespaceLootCategory.Materials)
                            continue;
                        originals.Add(reward, reward.Products);
                        reward.Products = mixedMaterials
                            ? new Dictionary<EntProtoId, int> { ["SheetSteel1"] = 150, ["SheetGlass1"] = units - 150 }
                            : new Dictionary<EntProtoId, int> { [product] = units };
                    }
                    OpenOutput(context);
                    harvester.Comp.StoredEnergy = harvester.Comp.EnergyPerReward;
                    produced = system.TryProduceReward(harvester);
                    repeated = system.TryProduceReward(harvester);
                });
                await server.WaitAssertion(() =>
                {
                    Assert.That(produced, Is.EqualTo(accepted));
                    Assert.That(repeated, Is.False);
                    Assert.That(RewardItems(context), Is.EqualTo(entities));
                    Assert.That(RewardUnits(context), Is.EqualTo(accepted ? units : 0));
                    Assert.That(harvester.Comp.StoredEnergy,
                        Is.EqualTo(accepted ? 0d : harvester.Comp.EnergyPerReward));
                    foreach (var uid in GetRewardEntities(context))
                    {
                        if (!server.EntMan.TryGetComponent<StackComponent>(uid, out var stack))
                            continue;
                        Assert.That(stack.Count, Is.InRange(1, server.System<StackSystem>().GetMaxCount(stack)));
                    }
                });
            }
            finally
            {
                await server.WaitPost(() =>
                {
                    foreach (var (reward, products) in originals)
                        reward.Products = products;
                });
            }
        });
    }

    /// <summary>Even a precharged harvester cannot emit rewards after losing any physical operating prerequisite.</summary>
    [TestCase("off")]
    [TestCase("no-receipt")]
    [TestCase("no-net")]
    [TestCase("unanchored")]
    [TestCase("paused")]
    [TestCase("container")]
    [TestCase("no-station")]
    [TestCase("nullspace")]
    [TestCase("critical")]
    public async Task HarvesterCannotSpendStoredEnergyWhenOperationIsInterrupted(string interruption)
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var entMan = server.EntMan;
            var harvester = await SpawnHarvester(context);
            var system = server.System<FishBluespaceHarvesterSystem>();
            await server.WaitPost(() =>
            {
                OpenOutput(context);
                harvester.Comp.StoredEnergy = 15_000_000d;
            });
            await server.WaitAssertion(() => Assert.That(system.CanProduceReward(harvester, out _), Is.True,
                "The fixture must be capable of producing a reward before its prerequisite is removed."));
            var produced = true;
            await server.WaitPost(() =>
            {
                switch (interruption)
                {
                    case "off":
                        system.TrySetEnabled(harvester, false, context.User);
                        break;
                    case "no-receipt":
                        entMan.GetComponent<PowerConsumerComponent>(harvester).NetworkLoad.ReceivingPower = 0f;
                        break;
                    case "no-net":
                        entMan.GetComponent<PowerConsumerComponent>(harvester).ClearNet();
                        break;
                    case "unanchored":
                        server.System<SharedTransformSystem>().Unanchor(harvester);
                        break;
                    case "paused":
                        server.System<MetaDataSystem>().SetEntityPaused(harvester, true);
                        break;
                    case "container":
                        var holder = entMan.SpawnEntity(null, context.FirstCoordinates);
                        var containers = server.System<SharedContainerSystem>();
                        containers.Insert((harvester.Owner, null, null, null), containers.EnsureContainer<Container>(holder, "test"));
                        break;
                    case "no-station":
                        server.System<StationSystem>().RemoveGridFromStation(context.FirstStation, context.FirstGrid);
                        break;
                    case "nullspace":
                        server.System<SharedTransformSystem>().DetachEntity(harvester);
                        break;
                    case "critical":
                        server.System<FishBluespaceBackgroundSystem>().TryAdjustLevel(harvester, 95f);
                        break;
                }
                produced = system.TryProduceReward(harvester);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(system.CanOperate(harvester), Is.False);
                Assert.That(produced, Is.False);
                Assert.That(harvester.Comp.StoredEnergy, Is.EqualTo(15_000_000d));
                Assert.That(RewardItems(context), Is.Zero);
            });
        });
    }

    /// <summary>Validated controls reject undefined selections and actors who have moved away from an open interface.</summary>
    [Test]
    public async Task HarvesterRejectsInvalidEnumsAndDistantControlActors()
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var harvester = await SpawnHarvester(context);
            var system = server.System<FishBluespaceHarvesterSystem>();
            var accepted = true;
            await server.WaitPost(() =>
            {
                accepted = system.TrySetPowerMode(harvester, (FishBluespacePowerMode) 255, context.User) |
                           system.TrySetLootCategory(harvester, (FishBluespaceLootCategory) 255, context.User);
                server.System<SharedTransformSystem>().SetCoordinates(context.User,
                    new EntityCoordinates(context.FirstGrid, 20, 20));
                accepted |= system.TrySetEnabled(harvester, false, context.User) |
                            system.TrySetPowerMode(harvester, FishBluespacePowerMode.Overdrive, context.User);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(accepted, Is.False);
                Assert.That(harvester.Comp.Enabled, Is.True);
                Assert.That(harvester.Comp.PowerMode, Is.EqualTo(FishBluespacePowerMode.Normal));
            });
        });
    }

    /// <summary>The station owns both incursion cooldown and the two-creature budget, even after deadlines are due.</summary>
    [Test]
    public async Task IncursionsRespectCooldownAndStationCreatureBudget()
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var harvester = await SpawnHarvester(context);
            var system = server.System<FishBluespaceHarvesterSystem>();
            var opened = false;
            var repeated = true;
            await server.WaitPost(() =>
            {
                OpenOutput(context);
                server.System<FishBluespaceBackgroundSystem>().TryAdjustLevel(harvester, 70f);
                opened = system.TryOpenIncursion(harvester);
                repeated = system.TryOpenIncursion(harvester);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(opened, Is.True);
                Assert.That(repeated, Is.False);
                var incidents = server.EntMan.GetComponent<FishBluespaceHarvesterStationComponent>(context.FirstStation);
                Assert.That(incidents.NextIncident, Is.GreaterThanOrEqualTo(server.Timing.CurTime + TimeSpan.FromSeconds(60)));
                Assert.That(incidents.Incursions, Has.Count.EqualTo(1));
            });
            await server.WaitPost(() =>
            {
                var incidents = server.EntMan.GetComponent<FishBluespaceHarvesterStationComponent>(context.FirstStation);
                incidents.NextIncident = server.Timing.CurTime;
                opened = system.TryOpenIncursion(harvester);
                incidents.NextIncident = server.Timing.CurTime;
                repeated = system.TryOpenIncursion(harvester);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(opened, Is.True);
                Assert.That(repeated, Is.False);
                Assert.That(server.EntMan.GetComponent<FishBluespaceHarvesterStationComponent>(context.FirstStation).Incursions,
                    Has.Count.EqualTo(2));
                Assert.That(server.EntMan.HasComponent<FishBluespaceHarvesterStationComponent>(context.SecondStation), Is.False);
            });
        });
    }

    private static async Task<Entity<FishBluespaceHarvesterComponent>> SpawnHarvester(StationContext context)
    {
        var server = context.Pair.Server;
        Entity<FishBluespaceHarvesterComponent> harvester = default;
        await server.WaitPost(() =>
        {
            server.EntMan.SpawnEntity("CableHV", context.FirstCoordinates);
            var uid = server.EntMan.SpawnEntity("FishBluespaceHarvester", context.FirstCoordinates);
            harvester = (uid, server.EntMan.GetComponent<FishBluespaceHarvesterComponent>(uid));
        });
        // Fish-edit: два тика завершают построение CableHV-группы и подключение нативного PowerConsumer.
        await server.WaitRunTicks(2);
        var opened = false;
        var enabled = false;
        await server.WaitPost(() =>
        {
            opened = server.System<SharedUserInterfaceSystem>().TryOpenUi(harvester.Owner,
                FishBluespaceDeviceUiKey.Key, context.User);
            var system = server.System<FishBluespaceHarvesterSystem>();
            enabled = system.TrySetEnabled(harvester, true, context.User);
            // Fish-edit: это управляемая выборка нагрузки, а не тест генератора или полной электросети.
            server.EntMan.GetComponent<PowerConsumerComponent>(harvester).NetworkLoad.ReceivingPower = 1_000_000f;
            system.Update(1f);
        });
        await server.WaitAssertion(() =>
        {
            Assert.That(opened && enabled, Is.True, "The fixture must enable the native machine through its real UI contract.");
            Assert.That(server.EntMan.GetComponent<PowerConsumerComponent>(harvester).Net, Is.Not.Null);
            Assert.That(harvester.Comp.StoredEnergy, Is.Zero, "The first changed-demand sample is deliberately skipped.");
            Assert.That(server.System<FishBluespaceHarvesterSystem>().CanOperate(harvester), Is.True);
        });
        return harvester;
    }

    private static void SetApcReceipt(StationContext context, EntityUid machine, float fraction)
    {
        var server = context.Pair.Server;
        var receiver = server.EntMan.GetComponent<ApcPowerReceiverComponent>(machine);
        // Fish-edit: задаём и флаг питания, и реальную мощность нагрузки; генерация и разводка APC здесь не проверяются.
        receiver.Powered = true;
        receiver.NetworkLoad.Enabled = true;
        receiver.NetworkLoad.ReceivingPower = server.EntMan.GetComponent<PowerStateComponent>(machine).WorkingPowerDraw * fraction;
    }

    private static void OpenOutput(StationContext context)
    {
        context.Pair.Server.System<SharedMapSystem>().SetTile(context.FirstGrid,
            new EntityCoordinates(context.FirstGrid, 2.5f, 0.5f), context.Floor);
    }

    private static int RewardItems(StationContext context)
    {
        return GetRewardEntities(context).Count;
    }

    private static int RewardUnits(StationContext context)
    {
        var server = context.Pair.Server;
        return GetRewardEntities(context).Sum(uid => server.System<StackSystem>().GetCount(uid));
    }

    private static List<EntityUid> GetRewardEntities(StationContext context)
    {
        var server = context.Pair.Server;
        var containers = server.System<SharedContainerSystem>();
        var products = server.ProtoMan.EnumeratePrototypes<FishBluespaceHarvesterLootPrototype>()
            .SelectMany(reward => reward.Products.Keys).Select(id => id.Id).ToHashSet();
        var entities = new List<EntityUid>();
        var query = server.EntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var meta, out var transform))
        {
            // Fish-edit: детали в machine_parts, предметы в руках и вложенных контейнерах не являются выданной добычей.
            if (transform.MapUid == context.FirstMap && meta.EntityPrototype is { } prototype && products.Contains(prototype.ID) &&
                !containers.IsEntityOrParentInContainer(uid, meta, transform))
                entities.Add(uid);
        }
        return entities;
    }

    private static async Task WithStations(Func<StationContext, Task> scenario)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var server = pair.Server;
        var first = await pair.CreateTestMap();
        var second = await pair.CreateTestMap();
        EntityUid firstStation = default;
        EntityUid secondStation = default;
        EntityUid user = default;
        var completed = false;
        try
        {
            await server.WaitPost(() =>
            {
                var map = server.System<SharedMapSystem>();
                for (var x = -1; x <= 1; x++)
                for (var y = -1; y <= 1; y++)
                {
                    map.SetTile(first.Grid, new EntityCoordinates(first.Grid, x, y), first.Tile.Tile);
                    map.SetTile(second.Grid, new EntityCoordinates(second.Grid, x, y), second.Tile.Tile);
                }
                firstStation = server.EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                secondStation = server.EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                server.EntMan.AddComponent<StationDataComponent>(firstStation);
                server.EntMan.AddComponent<StationDataComponent>(secondStation);
                server.System<StationSystem>().AddGridToStation(firstStation, first.Grid);
                server.System<StationSystem>().AddGridToStation(secondStation, second.Grid);
                user = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(first.Grid, 2.25f, 0.5f));
            });
            await scenario(new StationContext(pair, first.Grid, second.Grid, first.MapUid,
                firstStation, secondStation, user, first.Tile.Tile));
            completed = true;
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.EntMan.DeleteEntity(first.MapUid);
                server.EntMan.DeleteEntity(second.MapUid);
                server.EntMan.DeleteEntity(firstStation);
                server.EntMan.DeleteEntity(secondStation);
            });
            if (completed)
                await pair.CleanReturnAsync();
        }
    }

    private sealed record StationContext(TestPair Pair, Entity<MapGridComponent> FirstGrid,
        Entity<MapGridComponent> SecondGrid, EntityUid FirstMap, EntityUid FirstStation, EntityUid SecondStation,
        EntityUid User, Tile Floor)
    {
        public EntityCoordinates FirstCoordinates => new(FirstGrid, 0.5f, 0.5f);
        public EntityCoordinates SecondCoordinates => new(SecondGrid, 0.5f, 0.5f);
    }
}
