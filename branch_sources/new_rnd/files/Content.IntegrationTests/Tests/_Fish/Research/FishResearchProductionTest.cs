using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Server.Lathe;
using Content.Server.Materials;
using Content.Server.Power.EntitySystems;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Lathe;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Materials;
using Content.Shared.Morgue.Components;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Stacks;
using Content.Shared.Storage.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using ServerResearchSystem = Content.Server.Research.Systems.ResearchSystem;

namespace Content.IntegrationTests.Tests._Fish.Research;

[TestFixture, NonParallelizable]
public sealed class FishResearchProductionTest
{
    private static readonly ProtoId<LatheRecipePrototype> ProcessingRecipe = "FishProcessedUranium";
    private static readonly ProtoId<LatheRecipePrototype> ScalpelRecipe = "Scalpel";
    private static readonly ProtoId<LatheRecipePrototype> CrematoriumRecipe = "FishCrematoriumFlatpack";

    /// <summary>A fresh protolathe prints a surgical tool without research and preserves its static recipes.</summary>
    [Test]
    public async Task FreshProtolathePrintsStaticRecipeWithoutResearch()
    {
        await WithLathe("Protolathe", async context =>
        {
            var server = context.Pair.Server;
            var lathe = server.System<LatheSystem>();
            List<ProtoId<LatheRecipePrototype>> available = [];
            var inserted = false;
            var queued = false;
            var started = false;
            await server.WaitPost(() =>
            {
                available = lathe.GetAvailableRecipes(context.Lathe,
                    server.EntMan.GetComponent<LatheComponent>(context.Lathe));
                inserted = InsertRecipeMaterials(context, ScalpelRecipe, 1);
                queued = lathe.TryAddToQueue(context.Lathe, server.ProtoMan.Index(ScalpelRecipe), 1);
                started = lathe.TryStartProducing(context.Lathe);
                if (started)
                    lathe.FinishProducing(context.Lathe);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.GetComponent<TechnologyDatabaseComponent>(context.ResearchServer)
                    .UnlockedTechnologies, Is.Empty);
                foreach (var recipe in new[] { "ProtoSheetOfficePaper", "Beaker", "Scalpel" })
                    Assert.That(available, Does.Contain((ProtoId<LatheRecipePrototype>) recipe));
                Assert.That(inserted && queued && started, Is.True);
                var result = server.ProtoMan.Index(ScalpelRecipe).Result;
                Assert.That(result, Is.Not.Null);
                Assert.That(OutputEntities(context, result!.Value), Has.Count.EqualTo(1));
            });
        });
    }

    /// <summary>Research unlocks one printable flatpack that a multitool unpacks into an empty crematorium.</summary>
    [Test]
    public async Task ResearchedCrematoriumPrintsAndUnpacksIntoEmptyMachine()
    {
        await WithLathe("Protolathe", async context =>
        {
            var server = context.Pair.Server;
            var entMan = server.EntMan;
            var lathe = server.System<LatheSystem>();
            var inserted = false;
            var queuedBeforeResearch = false;
            await server.WaitPost(() =>
            {
                inserted = InsertRecipeMaterials(context, CrematoriumRecipe, 1);
                queuedBeforeResearch = lathe.TryAddToQueue(context.Lathe, server.ProtoMan.Index(CrematoriumRecipe), 1);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(inserted, Is.True);
                Assert.That(lathe.GetAvailableRecipes(context.Lathe, entMan.GetComponent<LatheComponent>(context.Lathe)),
                    Does.Not.Contain(CrematoriumRecipe), "A fresh protolathe must require research for the crematorium.");
                Assert.That(queuedBeforeResearch, Is.False, "Materials alone must not unlock the crematorium.");
                Assert.That(OutputEntities(context, "FishCrematoriumFlatpack"), Is.Empty);
            });

            var queued = false;
            var started = false;
            var diagnostics = string.Empty;
            await server.WaitPost(() =>
            {
                GrantRecipe(context, CrematoriumRecipe);
                diagnostics = DescribeProduction(context, CrematoriumRecipe);
                queued = lathe.TryAddToQueue(context.Lathe, server.ProtoMan.Index(CrematoriumRecipe), 1);
                started = lathe.TryStartProducing(context.Lathe);
                if (started)
                    lathe.FinishProducing(context.Lathe);
            });
            EntityUid flatpack = default;
            await server.WaitAssertion(() =>
            {
                Assert.That(lathe.GetAvailableRecipes(context.Lathe, entMan.GetComponent<LatheComponent>(context.Lathe)),
                    Does.Contain(CrematoriumRecipe));
                Assert.That(queued, Is.True, $"Queue rejected the researched recipe: {diagnostics}");
                Assert.That(started, Is.True, $"Production did not start: {diagnostics}");
                var outputs = OutputEntities(context, "FishCrematoriumFlatpack");
                Assert.That(outputs, Has.Count.EqualTo(1), "One print must create exactly one crematorium flatpack.");
                flatpack = outputs.Single();
                Assert.That(OutputEntities(context, "Crematorium"), Is.Empty);
            });

            var pickedUp = false;
            var interacted = false;
            await server.WaitPost(() =>
            {
                var map = server.System<SharedMapSystem>();
                Entity<MapGridComponent> grid = (context.Coordinates.EntityId,
                    entMan.GetComponent<MapGridComponent>(context.Coordinates.EntityId));
                var unpackCoordinates = new EntityCoordinates(grid, 1.5f, 0.5f);
                // Fish-edit: отдельная плитка оставляет место для машины рядом с принтером и сервером исследований.
                map.SetTile(grid, unpackCoordinates, map.GetTileRef(grid, context.Coordinates).Tile);
                server.System<SharedTransformSystem>().SetCoordinates(flatpack, unpackCoordinates);
                var multitool = entMan.SpawnEntity("Multitool", context.Coordinates);
                pickedUp = server.System<SharedHandsSystem>().TryPickupAnyHand(context.User, multitool);
                if (pickedUp)
                    interacted = server.System<SharedInteractionSystem>()
                        .InteractUsing(context.User, multitool, flatpack, unpackCoordinates);
            });
            await server.WaitRunTicks(1);
            await server.WaitAssertion(() =>
            {
                Assert.That(pickedUp && interacted, Is.True, "A held multitool must unpack the printed flatpack.");
                Assert.That(entMan.EntityExists(flatpack), Is.False, "Unpacking must consume the flatpack.");
                var crematoria = OutputEntities(context, "Crematorium");
                Assert.That(crematoria, Has.Count.EqualTo(1), "Unpacking must create exactly one native crematorium.");
                var crematorium = crematoria.Single();
                Assert.That(entMan.HasComponent<CrematoriumComponent>(crematorium), Is.True);
                Assert.That(entMan.GetComponent<EntityStorageComponent>(crematorium).Contents.ContainedEntities, Is.Empty,
                    "A newly unpacked crematorium must start with empty storage.");
            });
        });
    }

    /// <summary>Researched hardsuits print with a single attached helmet that can be equipped and retracted.</summary>
    [TestCase("Protolathe", "FishCargoHardsuits")]
    [TestCase("Protolathe", "FishEngineeringHardsuits")]
    [TestCase("Protolathe", "FishResearchHardsuits")]
    [TestCase("Protolathe", "FishServiceHardsuits")]
    [TestCase("MedicalTechFab", "FishMedicalHardsuits")]
    [TestCase("SecurityTechFab", "FishSecurityHardsuits")]
    public async Task ResearchedHardsuitsPrintWithUsableIntegratedHelmets(string machine, string pack)
    {
        await WithLathe(machine, async context =>
        {
            var server = context.Pair.Server;
            var entMan = server.EntMan;
            var lathe = server.System<LatheSystem>();
            var inventory = server.System<InventorySystem>();
            ProtoId<LatheRecipePrototype>[] recipes = [];
            await server.WaitAssertion(() =>
            {
                recipes = server.ProtoMan.Index<LatheRecipePackPrototype>(pack).Recipes.ToArray();
                Assert.That(recipes, Is.Not.Empty, $"{pack} must contain hardsuit recipes.");
                var available = lathe.GetAvailableRecipes(context.Lathe,
                    entMan.GetComponent<LatheComponent>(context.Lathe));
                foreach (var recipe in recipes)
                    Assert.That(available, Does.Not.Contain(recipe), $"{recipe} must require research.");
            });

            foreach (var recipe in recipes)
            {
                var inserted = false;
                var queued = false;
                var started = false;
                var diagnostics = string.Empty;
                await server.WaitPost(() =>
                {
                    GrantRecipe(context, recipe);
                    inserted = InsertRecipeMaterials(context, recipe, 1);
                    diagnostics = DescribeProduction(context, recipe);
                    queued = lathe.TryAddToQueue(context.Lathe, server.ProtoMan.Index(recipe), 1);
                    started = lathe.TryStartProducing(context.Lathe);
                    if (started)
                        lathe.FinishProducing(context.Lathe);
                });

                EntityUid suit = default;
                EntityUid helmet = default;
                await server.WaitAssertion(() =>
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(inserted, Is.True, $"Material insertion failed: {diagnostics}");
                        Assert.That(queued, Is.True, $"Queue rejected the recipe: {diagnostics}");
                        Assert.That(started, Is.True, $"Production did not start: {diagnostics}");
                    });
                    var result = server.ProtoMan.Index(recipe).Result;
                    Assert.That(result, Is.Not.Null, $"{recipe} must produce a physical hardsuit.");
                    var suits = OutputEntities(context, result!.Value);
                    Assert.That(suits, Has.Count.EqualTo(1), $"{recipe} must print exactly one hardsuit.");
                    suit = suits.Single();
                    var clothing = entMan.GetComponent<ToggleableClothingComponent>(suit);
                    Assert.That(clothing.ClothingUid, Is.Not.Null, $"{recipe} must include its helmet.");
                    helmet = clothing.ClothingUid!.Value;
                    Assert.That(entMan.EntityExists(helmet), Is.True, $"{recipe} helmet must exist.");
                    Assert.That(OutputEntities(context, clothing.ClothingPrototype), Is.EqualTo(new[] { helmet }),
                        $"{recipe} must create only its integrated helmet, without a loose duplicate.");
                    Assert.That(clothing.Container?.ContainedEntity, Is.EqualTo(helmet),
                        $"{recipe} helmet must start inside its hardsuit.");
                    Assert.That(entMan.GetComponent<AttachedClothingComponent>(helmet).AttachedUid, Is.EqualTo(suit),
                        $"{recipe} helmet must remain attached to its hardsuit.");
                });

                var equipped = false;
                await server.WaitPost(() =>
                {
                    equipped = inventory.TryEquip(context.User, suit, "outerClothing");
                    entMan.EventBus.RaiseLocalEvent(suit, new ToggleClothingEvent { Performer = context.User });
                });
                await server.WaitAssertion(() =>
                {
                    Assert.That(equipped, Is.True, $"{recipe} printed hardsuit must be wearable.");
                    inventory.TryGetSlotEntity(context.User, "head", out var wornHelmet);
                    Assert.That(wornHelmet, Is.EqualTo(helmet), $"{recipe} must deploy its own helmet.");
                });
                await server.WaitPost(() =>
                    entMan.EventBus.RaiseLocalEvent(suit, new ToggleClothingEvent { Performer = context.User }));
                await server.WaitAssertion(() =>
                {
                    Assert.That(entMan.GetComponent<ToggleableClothingComponent>(suit).Container?.ContainedEntity,
                        Is.EqualTo(helmet), $"{recipe} must retract the same helmet into its hardsuit.");
                    Assert.That(inventory.TryGetSlotEntity(context.User, "head", out _), Is.False,
                        $"{recipe} must leave the head slot empty after retracting its helmet.");
                });
                await server.WaitPost(() => entMan.DeleteEntity(suit));
                await server.WaitRunTicks(1);
            }
        });
    }

    /// <summary>Each printed uranium sheet adds exactly one unit, including when it merges into an existing stack.</summary>
    [Test]
    public async Task ProcessingAddsExactlyOneUnitPerPrintWhenStacksMerge()
    {
        await WithLathe("Protolathe", async context =>
        {
            var server = context.Pair.Server;
            var lathe = server.System<LatheSystem>();
            var inserted = false;
            var queued = false;
            var started = false;
            await server.WaitPost(() =>
            {
                server.EntMan.SpawnEntity("FishSheetProcessedUranium1", context.Coordinates);
                GrantRecipe(context, "FishProcessedUranium");
                inserted = InsertRecipeMaterials(context, "FishProcessedUranium", 6);
                queued = lathe.TryAddToQueue(context.Lathe,
                    server.ProtoMan.Index(ProcessingRecipe), 6);
                started = lathe.TryStartProducing(context.Lathe);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(inserted && queued && started, Is.True);
                Assert.That(OutputStacks(context).Sum(stack => stack.Count), Is.EqualTo(1));
            });

            for (var printed = 1; printed <= 6; printed++)
            {
                await server.WaitPost(() => lathe.FinishProducing(context.Lathe));
                await server.WaitRunTicks(1);
                await server.WaitAssertion(() =>
                {
                    var stacks = OutputStacks(context);
                    Assert.That(stacks.Sum(stack => stack.Count), Is.EqualTo(printed + 1));
                    if (printed > 1)
                        Assert.That(stacks.Count, Is.LessThan(printed + 1),
                            "The scenario must exercise output merging, including the already existing fuel unit.");
                });
            }
        });
    }

    /// <summary>Interrupted production creates no output until power returns and the saved recipe finishes.</summary>
    [TestCase(false)]
    [TestCase(true)]
    public async Task InterruptedProductionCreatesNoOutputUntilResumed(bool powerLoss)
    {
        await WithLathe("Protolathe", async context =>
        {
            var server = context.Pair.Server;
            var lathe = server.System<LatheSystem>();
            var queued = false;
            var started = false;
            await server.WaitPost(() =>
            {
                GrantRecipe(context, "FishProcessedUranium");
                InsertRecipeMaterials(context, "FishProcessedUranium", 1);
                queued = lathe.TryAddToQueue(context.Lathe,
                    server.ProtoMan.Index(ProcessingRecipe), 1);
                started = lathe.TryStartProducing(context.Lathe);
                if (powerLoss)
                {
                    server.System<PowerReceiverSystem>().SetNeedsPower(context.Lathe, true);
                    server.System<PowerReceiverSystem>().SetPowerDisabled(context.Lathe, true);
                }
                else
                    lathe.AbortProduction(context.Lathe);
            });
            await context.Pair.RunSeconds(0.3f);
            await server.WaitAssertion(() =>
            {
                Assert.That(queued && started, Is.True);
                Assert.That(OutputStacks(context), Is.Empty);
                Assert.That(server.EntMan.GetComponent<LatheComponent>(context.Lathe).CurrentRecipe, Is.Null);
            });
            await server.WaitPost(() =>
            {
                var power = server.System<PowerReceiverSystem>();
                power.SetPowerDisabled(context.Lathe, false);
                power.SetNeedsPower(context.Lathe, false);
            });
            await server.WaitRunTicks(1);
            ProtoId<LatheRecipePrototype>? resumedRecipe = null;
            await server.WaitPost(() =>
            {
                var component = server.EntMan.GetComponent<LatheComponent>(context.Lathe);
                // Fish-edit: восстановление питания уже могло запустить сохранённую очередь через PowerChangedEvent.
                if (component.CurrentRecipe == null)
                    lathe.TryStartProducing(context.Lathe);
                resumedRecipe = component.CurrentRecipe;
                if (resumedRecipe == ProcessingRecipe)
                    lathe.FinishProducing(context.Lathe);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(resumedRecipe, Is.EqualTo(ProcessingRecipe),
                    "Restored power or an explicit restart must resume the interrupted recipe.");
                Assert.That(OutputStacks(context).Sum(stack => stack.Count), Is.EqualTo(1));
            });
        });
    }

    private static void GrantRecipe(LatheContext context, string recipe)
    {
        var server = context.Pair.Server;
        foreach (var technology in server.ProtoMan.EnumeratePrototypes<TechnologyPrototype>())
        {
            if (technology.RecipeUnlocks.Contains((ProtoId<LatheRecipePrototype>) recipe))
                server.System<ServerResearchSystem>().AddTechnology(context.ResearchServer, technology);
        }
    }

    private static bool InsertRecipeMaterials(LatheContext context, string recipe, int quantity)
    {
        var server = context.Pair.Server;
        var materials = server.System<MaterialStorageSystem>();
        var inserted = true;
        foreach (var (material, amount) in server.ProtoMan.Index<LatheRecipePrototype>(recipe).Materials)
        {
            var materialPrototype = server.ProtoMan.Index(material);
            if (materialPrototype.StackEntity is not { } stackEntity ||
                !server.ProtoMan.Index(stackEntity).TryComp<PhysicalCompositionComponent>(out var composition,
                    server.EntMan.ComponentFactory))
                return false;
            var perSheet = composition.MaterialComposition.GetValueOrDefault(material);
            if (perSheet <= 0)
                return false;
            // Fish-edit: физические листы неделимы, поэтому для стоимости 250 нужны три листа по 100.
            var supplied = (int) Math.Ceiling((double) amount * quantity / perSheet) * perSheet;
            var sheets = materials.SpawnMultipleFromMaterial(supplied, materialPrototype, context.Coordinates);
            inserted &= sheets.Count != 0;
            foreach (var sheet in sheets)
                inserted &= materials.TryInsertMaterialEntity(context.User, sheet, context.Lathe);
        }
        return inserted;
    }

    private static string DescribeProduction(LatheContext context, string recipe)
    {
        var server = context.Pair.Server;
        var component = server.EntMan.GetComponent<LatheComponent>(context.Lathe);
        var lathe = server.System<LatheSystem>();
        var materials = server.System<MaterialStorageSystem>();
        var costs = server.ProtoMan.Index<LatheRecipePrototype>(recipe).Materials.Select(material =>
            $"{material.Key}: stored={materials.GetMaterialAmount(context.Lathe, material.Key)}, baseCost={material.Value}");
        return $"recipe={recipe}, available={lathe.GetAvailableRecipes(context.Lathe, component).Contains(recipe)}, " +
               $"powered={server.System<PowerReceiverSystem>().IsPowered(context.Lathe)}, " +
               $"current={component.CurrentRecipe}, queuedBatches={component.Queue.Count}; {string.Join(", ", costs)}";
    }

    private static List<EntityUid> OutputEntities(LatheContext context, EntProtoId prototype)
    {
        var results = new List<EntityUid>();
        var entMan = context.Pair.Server.EntMan;
        var map = entMan.GetComponent<TransformComponent>(context.Lathe).MapUid;
        var query = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var metadata, out var transform))
        {
            if (metadata.EntityPrototype?.ID == prototype.Id && transform.MapUid == map)
                results.Add(uid);
        }
        return results;
    }

    private static List<StackComponent> OutputStacks(LatheContext context)
    {
        var results = new List<StackComponent>();
        var entMan = context.Pair.Server.EntMan;
        var map = entMan.GetComponent<TransformComponent>(context.Lathe).MapUid;
        var query = entMan.EntityQueryEnumerator<StackComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out _, out var stack, out var metadata, out var transform))
        {
            if (metadata.EntityPrototype?.ID == "FishSheetProcessedUranium1" && transform.MapUid == map)
                results.Add(stack);
        }
        return results;
    }

    private static async Task WithLathe(string prototype, Func<LatheContext, Task> scenario)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid researchServer = default;
        EntityUid lathe = default;
        EntityUid user = default;
        var completed = false;
        try
        {
            await server.WaitPost(() =>
            {
                researchServer = server.EntMan.SpawnEntity("ResearchAndDevelopmentServer", map.GridCoords);
                lathe = server.EntMan.SpawnEntity(prototype, map.GridCoords);
                user = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
                var power = server.System<PowerReceiverSystem>();
                power.SetNeedsPower(researchServer, false);
                power.SetNeedsPower(lathe, false);
                server.System<ServerResearchSystem>().RegisterClient(lathe, researchServer);
            });
            await server.WaitRunTicks(1);
            await scenario(new LatheContext(pair, researchServer, lathe, user, map.GridCoords));
            completed = true;
        }
        finally
        {
            await server.WaitPost(() => server.EntMan.DeleteEntity(map.MapUid));
            if (completed)
                await pair.CleanReturnAsync();
            else
                await pair.DisposeAsync();
        }
    }

    private sealed record LatheContext(TestPair Pair, EntityUid ResearchServer, EntityUid Lathe, EntityUid User,
        EntityCoordinates Coordinates);
}
