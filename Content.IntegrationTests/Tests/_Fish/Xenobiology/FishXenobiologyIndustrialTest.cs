using System.Collections.Generic;
using Content.Server._Fish.Xenobiology;
using Content.Server.Lathe;
using Content.Server.NPC.HTN;
using Content.Server.Power.EntitySystems;
using Content.Shared._Fish.Xenobiology;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Lathe;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Research.Prototypes;
using Content.Shared.Stacks;
using Content.Shared.Temperature.Components;
using Content.Shared.Temperature.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using ServerResearchSystem = Content.Server.Research.Systems.ResearchSystem;

namespace Content.IntegrationTests.Tests._Fish.Xenobiology;

/// <summary>Exercises the industrial research gate, irreversible induction and interruptible fueled production.</summary>
[TestFixture, NonParallelizable]
public sealed class FishXenobiologyIndustrialTest
{
    /// <summary>The research gate changes the catalyst's actual availability on a linked protolathe.</summary>
    [Test]
    public async Task IndustrialResearchUnlocksCatalystOnProtolathe()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            EntityUid lathe = default;
            EntityUid research = default;
            await server.WaitPost(() =>
            {
                lathe = server.EntMan.SpawnEntity("Protolathe", lab.Coordinates);
                research = server.EntMan.SpawnEntity("ResearchAndDevelopmentServer", lab.Coordinates);
                server.System<PowerReceiverSystem>().SetNeedsPower(lathe, false);
                server.System<PowerReceiverSystem>().SetNeedsPower(research, false);
                server.System<ServerResearchSystem>().RegisterClient(lathe, research);
            });
            await server.WaitRunTicks(1);
            await server.WaitAssertion(() => Assert.That(server.System<LatheSystem>().GetAvailableRecipes(lathe,
                    server.EntMan.GetComponent<LatheComponent>(lathe)),
                Does.Not.Contain((ProtoId<LatheRecipePrototype>) "FishXenoIndustrialCatalyst")));
            await server.WaitPost(() =>
                server.System<ServerResearchSystem>().AddTechnology(research, "FishIndustrialXenobiology"));
            await server.WaitAssertion(() => Assert.That(server.System<LatheSystem>().GetAvailableRecipes(lathe,
                    server.EntMan.GetComponent<LatheComponent>(lathe)),
                Does.Contain((ProtoId<LatheRecipePrototype>) "FishXenoIndustrialCatalyst")));
        });
    }

    /// <summary>Induction excludes both a valid division and later corpse harvesting from the same lineage.</summary>
    [Test]
    public async Task InductionBlocksCompetingDivisionAndCorpseHarvest()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            var entMan = server.EntMan;
            var lifecycle = server.System<FishXenobiologySystem>();
            EntityUid slime = default;
            var divideBefore = false;
            var induced = false;
            var divideAfter = true;
            var harvest = true;
            var controlHarvest = false;
            await server.WaitPost(() =>
            {
                slime = SpawnAdult(lab);
                var state = entMan.GetComponent<FishXenobiologySlimeComponent>(slime);
                state.Biomass = 100f;
                var temperature = entMan.GetComponent<TemperatureComponent>(slime);
                server.System<SharedTemperatureSystem>().ChangeHeat((slime, temperature),
                    (293.15f - temperature.Temperature) * temperature.HeatCapacity, ignoreHeatResistance: true);
                server.System<SatiationSystem>().SetValue((slime, entMan.GetComponent<SatiationComponent>(slime)),
                    SatiationSystem.Hunger, 100f);
                divideBefore = lifecycle.CanDivide(slime);
                var catalyst = entMan.SpawnEntity("FishXenoIndustrialCatalyst", lab.Coordinates);
                server.System<SharedHandsSystem>().TryPickupAnyHand(lab.User, catalyst);
                induced = server.System<FishXenobiologyIndustrialSystem>().TryInduce(catalyst, lab.User, slime);
                divideAfter = lifecycle.CanDivide(slime);
                server.System<MobStateSystem>().ChangeMobState(slime, MobState.Dead);
                harvest = lifecycle.TryHarvest(slime, out _, out _);
                var control = SpawnAdult(lab);
                server.System<MobStateSystem>().ChangeMobState(control, MobState.Dead);
                controlHarvest = lifecycle.CanHarvest(control);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(divideBefore && induced && controlHarvest, Is.True,
                    "The adult must otherwise qualify for division, and an ordinary adult corpse for harvesting.");
                Assert.That(divideAfter || harvest, Is.False);
                Assert.That(entMan.GetComponent<FishXenobiologyIndustrialInductionComponent>(slime).Consumed, Is.False);
            });
        });
    }

    /// <summary>Ten individually held inputs transform one adult exactly once, including repeated same-tick attempts.</summary>
    [Test]
    public async Task TenUnusedExtractsTransformOneInducedAdultExactlyOnce()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            var entMan = server.EntMan;
            var industrial = server.System<FishXenobiologyIndustrialSystem>();
            EntityUid slime = default;
            var held = false;
            var induced = false;
            var repeated = true;
            await server.WaitPost(() =>
            {
                slime = SpawnAdult(lab);
                var catalyst = entMan.SpawnEntity("FishXenoIndustrialCatalyst", lab.Coordinates);
                held = server.System<SharedHandsSystem>().TryPickupAnyHand(lab.User, catalyst);
                induced = industrial.TryInduce(catalyst, lab.User, slime);
                repeated = industrial.TryInduce(catalyst, lab.User, slime);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(held && induced, Is.True);
                Assert.That(repeated, Is.False);
                Assert.That(entMan.GetComponent<FishXenobiologyIndustrialInductionComponent>(slime).ExtractsFed, Is.Zero);
            });
            await server.WaitRunTicks(1);
            for (var index = 0; index < 10; index++)
            {
                var fed = false;
                await server.WaitPost(() =>
                {
                    var extract = entMan.SpawnEntity("FishXenoExtractMetal", lab.Coordinates);
                    held = server.System<SharedHandsSystem>().TryPickupAnyHand(lab.User, extract);
                    fed = industrial.TryFeed(extract, lab.User, slime);
                    repeated = industrial.TryFeed(extract, lab.User, slime);
                });
                await server.WaitAssertion(() =>
                {
                    Assert.That(held && fed, Is.True);
                    Assert.That(repeated, Is.False, "A consumed held extract cannot be counted twice before deletion.");
                    Assert.That(lab.CountPrototype("FishXenoIndustrialExtractMetal"), Is.EqualTo(index == 9 ? 1 : 0));
                });
                await server.WaitRunTicks(1);
            }
            await server.WaitAssertion(() =>
            {
                Assert.That(entMan.EntityExists(slime), Is.False);
                Assert.That(lab.CountPrototype("FishXenoIndustrialExtractMetal"), Is.EqualTo(1));
                Assert.That(lab.CountPrototype("FishXenoExtractMetal"), Is.Zero);
            });
        });
    }

    /// <summary>Unsafe induction never consumes its catalyst or attaches progress to the specimen.</summary>
    [TestCase("juvenile")]
    [TestCase("mind")]
    [TestCase("contained")]
    [TestCase("paused")]
    [TestCase("distant")]
    [TestCase("unheld")]
    public async Task InductionRejectsUnsafeSpecimensWithoutConsumingCatalyst(string condition)
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            var entMan = server.EntMan;
            EntityUid slime = default;
            EntityUid catalyst = default;
            EntityUid mind = default;
            var held = false;
            var induced = true;
            try
            {
                await server.WaitPost(() =>
                {
                    slime = condition == "juvenile"
                        ? entMan.SpawnEntity("FishXenoSlimeMetal", lab.Coordinates)
                        : SpawnAdult(lab);
                    catalyst = entMan.SpawnEntity("FishXenoIndustrialCatalyst", lab.Coordinates);
                    held = condition == "unheld" || server.System<SharedHandsSystem>().TryPickupAnyHand(lab.User, catalyst);
                    switch (condition)
                    {
                        case "mind":
                            var minds = server.System<SharedMindSystem>();
                            mind = minds.CreateMind(null);
                            minds.TransferTo(mind, slime);
                            break;
                        case "contained":
                            var holder = entMan.SpawnEntity(null, lab.Coordinates);
                            var contents = server.System<SharedContainerSystem>().EnsureContainer<Container>(holder, "test");
                            server.System<SharedContainerSystem>().Insert(slime, contents);
                            break;
                        case "paused":
                            server.System<MetaDataSystem>().SetEntityPaused(slime, true);
                            break;
                        case "distant":
                            server.System<SharedTransformSystem>().SetCoordinates(slime,
                                new EntityCoordinates(lab.Coordinates.EntityId, 20, 20));
                            break;
                    }
                    induced = server.System<FishXenobiologyIndustrialSystem>().TryInduce(catalyst, lab.User, slime);
                });
                await server.WaitAssertion(() =>
                {
                    Assert.That(held, Is.True);
                    Assert.That(induced, Is.False);
                    Assert.That(entMan.HasComponent<FishXenobiologyIndustrialInductionComponent>(slime), Is.False);
                    Assert.That(entMan.GetComponent<FishXenobiologyIndustrialCatalystComponent>(catalyst).Used, Is.False);
                });
            }
            finally
            {
                if (mind.IsValid())
                    await server.WaitPost(() => entMan.DeleteEntity(mind));
            }
        });
    }

    /// <summary>Spent and mismatched held extracts cannot advance an otherwise valid induction.</summary>
    [TestCase(true)]
    [TestCase(false)]
    public async Task InductionRejectsUsedOrWrongBreedExtracts(bool used)
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            var entMan = server.EntMan;
            EntityUid slime = default;
            EntityUid extract = default;
            var induced = false;
            var held = false;
            var fed = true;
            await server.WaitPost(() =>
            {
                slime = SpawnAdult(lab);
                var catalyst = entMan.SpawnEntity("FishXenoIndustrialCatalyst", lab.Coordinates);
                server.System<SharedHandsSystem>().TryPickupAnyHand(lab.User, catalyst);
                induced = server.System<FishXenobiologyIndustrialSystem>().TryInduce(catalyst, lab.User, slime);
                extract = entMan.SpawnEntity(used ? "FishXenoExtractMetal" : "FishXenoExtractYellow", lab.Coordinates);
                entMan.GetComponent<FishXenobiologyExtractComponent>(extract).Used = used;
                held = server.System<SharedHandsSystem>().TryPickupAnyHand(lab.User, extract);
                fed = server.System<FishXenobiologyIndustrialSystem>().TryFeed(extract, lab.User, slime);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(induced && held, Is.True);
                Assert.That(fed, Is.False);
                Assert.That(entMan.GetComponent<FishXenobiologyIndustrialInductionComponent>(slime).ExtractsFed, Is.Zero);
                Assert.That(entMan.GetComponent<FishXenobiologyExtractComponent>(extract).Used, Is.EqualTo(used));
                Assert.That(lab.CountPrototype("FishXenoIndustrialExtractMetal"), Is.Zero);
            });
        });
    }

    /// <summary>Fuel pays for one due batch; interruptions discard old deadlines and never create catch-up output.</summary>
    [TestCase("hand")]
    [TestCase("nested")]
    [TestCase("space")]
    [TestCase("nullspace")]
    [TestCase("paused")]
    public async Task ProductionConsumesFuelOnceAndRestartsAfterInterruption(string interruption)
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            var entMan = server.EntMan;
            var industrial = server.System<FishXenobiologyIndustrialSystem>();
            var solutions = server.System<SharedSolutionContainerSystem>();
            EntityUid extract = default;
            EntityUid unusedExtract = default;
            EntityUid holder = default;
            var fueled = false;
            var early = true;
            var produced = false;
            var repeated = true;
            await server.WaitPost(() =>
            {
                extract = entMan.SpawnEntity("FishXenoIndustrialExtractMetal", lab.Coordinates);
                unusedExtract = entMan.SpawnEntity("FishXenoIndustrialExtractMetal", lab.Coordinates);
                if (solutions.TryGetSolution(extract, "extract", out var solution, out _))
                    fueled = solutions.TryAddReagent(solution.Value, "Plasma", FixedPoint2.New(15));
                early = industrial.TryProduce(extract);
                entMan.GetComponent<FishXenobiologyIndustrialActiveComponent>(extract).NextProduction =
                    server.Timing.CurTime - TimeSpan.FromMinutes(5);
                produced = industrial.TryProduce(extract);
                repeated = industrial.TryProduce(extract);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(fueled && produced, Is.True);
                Assert.That(early || repeated, Is.False);
                Assert.That(lab.CountPrototype("SheetSteel1"), Is.EqualTo(1));
                Assert.That(lab.CountPrototype("SheetGlass1"), Is.EqualTo(1));
                solutions.TryGetSolution(extract, "extract", out _, out var mixture);
                Assert.That(mixture.GetTotalPrototypeQuantity("Plasma"), Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(entMan.HasComponent<FishXenobiologyExtractComponent>(extract), Is.False);
                solutions.TryGetSolution(unusedExtract, "extract", out _, out var unusedMixture);
                Assert.That(unusedMixture.Volume, Is.EqualTo(FixedPoint2.Zero),
                    "Fuel and solution mutations must remain local to the fueled extract instance.");
            });

            var interrupted = true;
            await server.WaitPost(() =>
            {
                entMan.GetComponent<FishXenobiologyIndustrialActiveComponent>(extract).NextProduction = server.Timing.CurTime;
                switch (interruption)
                {
                    case "hand":
                        server.System<SharedHandsSystem>().TryPickupAnyHand(lab.User, extract);
                        break;
                    case "nested":
                        holder = entMan.SpawnEntity(null, lab.Coordinates);
                        var inner = entMan.SpawnEntity(null, lab.Coordinates);
                        var containers = server.System<SharedContainerSystem>();
                        containers.Insert(extract, containers.EnsureContainer<Container>(inner, "inner"));
                        containers.Insert(inner, containers.EnsureContainer<Container>(holder, "outer"));
                        break;
                    case "space":
                        server.System<SharedTransformSystem>().SetCoordinates(extract,
                            new EntityCoordinates(lab.Coordinates.EntityId, 20, 20));
                        break;
                    case "nullspace":
                        server.System<SharedTransformSystem>().DetachEntity(extract);
                        break;
                    case "paused":
                        server.System<MetaDataSystem>().SetEntityPaused(extract, true);
                        break;
                }
                interrupted = industrial.TryProduce(extract);
            });
            await server.WaitRunTicks(1);
            await server.WaitAssertion(() =>
            {
                Assert.That(interrupted, Is.False);
                Assert.That(industrial.CanStartProduction(extract), Is.False);
                Assert.That(entMan.GetComponent<FishXenobiologyIndustrialActiveComponent>(extract).NextProduction, Is.Null);
                Assert.That(lab.CountPrototype("SheetSteel1"), Is.EqualTo(1));
            });

            var restarted = false;
            await server.WaitPost(() =>
            {
                if (interruption == "hand")
                    server.System<SharedHandsSystem>().TryDrop((lab.User, null), extract);
                else if (interruption == "nested")
                {
                    var containers = server.System<SharedContainerSystem>();
                    if (containers.TryGetContainingContainer(extract, out var contents))
                        containers.Remove(extract, contents, destination: lab.Coordinates);
                    entMan.DeleteEntity(holder);
                }
                server.System<MetaDataSystem>().SetEntityPaused(extract, false);
                server.System<SharedTransformSystem>().SetCoordinates(extract, lab.Coordinates);
                restarted = industrial.TryStartProduction(extract);
                repeated = industrial.TryProduce(extract);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(restarted, Is.True);
                Assert.That(repeated, Is.False);
                Assert.That(entMan.GetComponent<FishXenobiologyIndustrialActiveComponent>(extract).NextProduction,
                    Is.GreaterThan(server.Timing.CurTime + TimeSpan.FromSeconds(59)));
                Assert.That(lab.CountPrototype("SheetSteel1"), Is.EqualTo(1));
                solutions.TryGetSolution(extract, "extract", out _, out var mixture);
                Assert.That(mixture.GetTotalPrototypeQuantity("Plasma"), Is.EqualTo(FixedPoint2.New(10)));
            });
        });
    }

    /// <summary>A multi-unit industrial recipe emits one native stack per material and charges fuel only once.</summary>
    [Test]
    public async Task IndustrialProductsUseUnitCountsAndNativeStacks()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            var recipe = server.ProtoMan.Index((ProtoId<FishXenobiologyIndustrialRecipePrototype>) "FishXenoIndustrialMetal");
            var originalProducts = recipe.Products;
            var produced = false;
            var repeated = true;
            EntityUid extract = default;
            try
            {
                await server.WaitPost(() =>
                {
                    recipe.Products = new Dictionary<EntProtoId, int> { ["SheetSteel1"] = 4, ["SheetGlass1"] = 3 };
                    extract = server.EntMan.SpawnEntity("FishXenoIndustrialExtractMetal", lab.Coordinates);
                    var solutions = server.System<SharedSolutionContainerSystem>();
                    if (solutions.TryGetSolution(extract, "extract", out var solution, out _))
                        solutions.TryAddReagent(solution.Value, "Plasma", FixedPoint2.New(10));
                    server.EntMan.GetComponent<FishXenobiologyIndustrialActiveComponent>(extract).NextProduction = server.Timing.CurTime;
                    produced = server.System<FishXenobiologyIndustrialSystem>().TryProduce(extract);
                    repeated = server.System<FishXenobiologyIndustrialSystem>().TryProduce(extract);
                });
                await server.WaitAssertion(() =>
                {
                    Assert.That(produced, Is.True);
                    Assert.That(repeated, Is.False);
                    Assert.That(lab.CountPrototype("SheetSteel1"), Is.EqualTo(1));
                    Assert.That(lab.CountPrototype("SheetGlass1"), Is.EqualTo(1));
                    var quantities = server.EntMan.EntityQueryEnumerator<StackComponent, MetaDataComponent, TransformComponent>();
                    var totals = new Dictionary<string, int>();
                    while (quantities.MoveNext(out _, out var stack, out var meta, out var transform))
                    {
                        if (transform.MapUid != lab.Map || meta.EntityPrototype?.ID is not { } id ||
                            id is not ("SheetSteel1" or "SheetGlass1"))
                            continue;
                        totals[id] = totals.GetValueOrDefault(id) + stack.Count;
                    }
                    Assert.That(totals.GetValueOrDefault("SheetSteel1"), Is.EqualTo(4));
                    Assert.That(totals.GetValueOrDefault("SheetGlass1"), Is.EqualTo(3));
                    server.System<SharedSolutionContainerSystem>().TryGetSolution(extract, "extract", out _, out var mixture);
                    Assert.That(mixture.GetTotalPrototypeQuantity("Plasma"), Is.EqualTo(FixedPoint2.New(5)));
                });
            }
            finally
            {
                await server.WaitPost(() => recipe.Products = originalProducts);
            }
        });
    }

    private static EntityUid SpawnAdult(FishXenobiologyTestLab lab)
    {
        var server = lab.Pair.Server;
        var slime = server.EntMan.SpawnEntity("FishXenoSlimeMetalAdult", lab.Coordinates);
        server.System<HTNSystem>().SetHTNEnabled((slime, server.EntMan.GetComponent<HTNComponent>(slime)), false);
        return slime;
    }
}
