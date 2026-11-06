using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Server.Lathe;
using Content.Server.Materials;
using Content.Server.Power.EntitySystems;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using ServerResearchSystem = Content.Server.Research.Systems.ResearchSystem;

namespace Content.IntegrationTests.Tests._Fish.Research;

[TestFixture, NonParallelizable]
public sealed class FishResearchMaterialsTest
{
    private const string ProcessedMaterial = "FishProcessedUranium";
    private const string ProcessedSheet = "FishSheetProcessedUranium1";
    private static readonly ProtoId<LatheRecipePrototype> ProcessingRecipe = "FishProcessedUranium";
    private static readonly ProtoId<LatheRecipePrototype> MicroreactorRecipe = "PowerCellMicroreactor";

    /// <summary>Ordinary uranium cannot pay for the processed fuel required by a microreactor.</summary>
    [Test]
    public async Task RawUraniumCannotPayForMicroreactor()
    {
        await WithLathe("Protolathe", async context =>
        {
            var server = context.Pair.Server;
            var lathe = server.System<LatheSystem>();
            var materials = server.System<MaterialStorageSystem>();
            var inserted = false;
            var canProduce = false;
            var queued = false;
            await server.WaitPost(() =>
            {
                server.System<ServerResearchSystem>().AddTechnology(context.ResearchServer, "FishMicroreactors");
                inserted = InsertMaterial(context, "Steel", 500) & InsertMaterial(context, "Glass", 400) &
                           InsertMaterial(context, "Gold", 100) & InsertMaterial(context, "Uranium", 200);
                var recipe = server.ProtoMan.Index(MicroreactorRecipe);
                canProduce = lathe.CanProduce(context.Lathe, recipe);
                queued = lathe.TryAddToQueue(context.Lathe, recipe, 1);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(inserted, Is.True);
                Assert.That(lathe.GetAvailableRecipes(context.Lathe, server.EntMan.GetComponent<LatheComponent>(context.Lathe)),
                    Does.Contain(MicroreactorRecipe));
                Assert.That(canProduce, Is.False);
                Assert.That(queued, Is.False);
                Assert.That(materials.GetMaterialAmount(context.Lathe, "Uranium"), Is.EqualTo(200));
                Assert.That(materials.GetMaterialAmount(context.Lathe, "Steel"), Is.EqualTo(500));
                Assert.That(materials.GetMaterialAmount(context.Lathe, ProcessedMaterial), Is.Zero);
            });
        });
    }

    /// <summary>Both protolathes unlock processing through research and spend the full uranium cost for one physical sheet.</summary>
    [TestCase("Protolathe")]
    [TestCase("ProtolatheHyperConvection")]
    public async Task ProcessingRequiresResearchAndSpendsTwoRawSheetsForOneProcessedSheet(string lathePrototype)
    {
        await WithLathe(lathePrototype, async context =>
        {
            var server = context.Pair.Server;
            var lathe = server.System<LatheSystem>();
            var materials = server.System<MaterialStorageSystem>();
            var inserted = false;
            var queuedBeforeResearch = false;
            var queued = false;
            var started = false;
            List<EntityUid> printed = [];
            await server.WaitPost(() =>
            {
                inserted = InsertMaterial(context, "Uranium", 200);
                queuedBeforeResearch = lathe.TryAddToQueue(context.Lathe, server.ProtoMan.Index(ProcessingRecipe), 1);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(inserted, Is.True);
                Assert.That(queuedBeforeResearch, Is.False);
                Assert.That(materials.GetMaterialAmount(context.Lathe, "Uranium"), Is.EqualTo(200));
                if (lathePrototype == "ProtolatheHyperConvection")
                    Assert.That(server.EntMan.GetComponent<LatheComponent>(context.Lathe).MaterialUseMultiplier, Is.LessThan(1f));
            });
            await server.WaitPost(() =>
            {
                server.System<ServerResearchSystem>().AddTechnology(context.ResearchServer, "FishUraniumProcessing");
                queued = lathe.TryAddToQueue(context.Lathe, server.ProtoMan.Index(ProcessingRecipe), 1);
                started = lathe.TryStartProducing(context.Lathe);
                if (started)
                    lathe.FinishProducing(context.Lathe);
                printed = FindOutputs(context, ProcessedSheet);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(queued, Is.True);
                Assert.That(started, Is.True);
                Assert.That(materials.GetMaterialAmount(context.Lathe, "Uranium"), Is.Zero,
                    "The conversion must spend 200 raw units even when the machine has a material discount.");
                Assert.That(printed, Has.Count.EqualTo(1));
                Assert.That(server.EntMan.GetComponent<StackComponent>(printed[0]).Count, Is.EqualTo(1));
                AssertProcessedComposition(context, printed[0]);
            });
        });
    }

    /// <summary>Printed fuel survives insertion and ejection without growth, then is consumed by an actual microreactor print.</summary>
    [Test]
    public async Task ProcessedFuelCanBePrintedEjectedReinsertedAndConsumed()
    {
        await WithLathe("Protolathe", async context =>
        {
            var server = context.Pair.Server;
            var lathe = server.System<LatheSystem>();
            var materials = server.System<MaterialStorageSystem>();
            var inserted = false;
            var queuedProcessing = false;
            var startedProcessing = false;
            var insertedProcessed = true;
            var reinserted = true;
            var queuedMicroreactor = false;
            var startedMicroreactor = false;
            List<EntityUid> printed = [];
            List<EntityUid> ejected = [];

            await server.WaitPost(() =>
            {
                var research = server.System<ServerResearchSystem>();
                research.AddTechnology(context.ResearchServer, "FishUraniumProcessing");
                research.AddTechnology(context.ResearchServer, "FishMicroreactors");
                inserted = InsertMaterial(context, "Uranium", 400) & InsertMaterial(context, "Steel", 500) &
                           InsertMaterial(context, "Glass", 400) & InsertMaterial(context, "Gold", 100);
                queuedProcessing = lathe.TryAddToQueue(context.Lathe, server.ProtoMan.Index(ProcessingRecipe), 2);
                startedProcessing = lathe.TryStartProducing(context.Lathe);
                // Fish-edit: завершаем публичный цикл печати без ожидания таймера, сохраняя настоящую очередь и расход материалов.
                if (startedProcessing)
                {
                    lathe.FinishProducing(context.Lathe);
                    lathe.FinishProducing(context.Lathe);
                }
                printed = FindOutputs(context, ProcessedSheet);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(inserted, Is.True);
                Assert.That(queuedProcessing, Is.True);
                Assert.That(startedProcessing, Is.True);
                Assert.That(printed.Sum(uid => server.EntMan.GetComponent<StackComponent>(uid).Count), Is.EqualTo(2));
                foreach (var sheet in printed)
                    AssertProcessedComposition(context, sheet);
                Assert.That(materials.GetMaterialAmount(context.Lathe, "Uranium"), Is.Zero);
            });
            await server.WaitPost(() =>
            {
                foreach (var sheet in printed)
                    insertedProcessed &= materials.TryInsertMaterialEntity(context.User, sheet, context.Lathe);
                ejected = materials.EjectMaterial(context.Lathe, ProcessedMaterial, maxAmount: 100);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(insertedProcessed, Is.True);
                Assert.That(materials.GetMaterialAmount(context.Lathe, ProcessedMaterial), Is.EqualTo(100));
                Assert.That(ejected.Sum(uid => server.EntMan.GetComponent<StackComponent>(uid).Count), Is.EqualTo(1));
                foreach (var sheet in ejected)
                    AssertProcessedComposition(context, sheet);
            });
            await server.WaitPost(() =>
            {
                foreach (var sheet in ejected)
                    reinserted &= materials.TryInsertMaterialEntity(context.User, sheet, context.Lathe);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(reinserted, Is.True);
                Assert.That(materials.GetMaterialAmount(context.Lathe, ProcessedMaterial), Is.EqualTo(200));
            });
            await server.WaitRunTicks(1);
            await server.WaitPost(() =>
            {
                queuedMicroreactor = lathe.TryAddToQueue(context.Lathe, server.ProtoMan.Index(MicroreactorRecipe), 1);
                startedMicroreactor = lathe.TryStartProducing(context.Lathe);
                if (startedMicroreactor)
                    lathe.FinishProducing(context.Lathe);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(queuedMicroreactor, Is.True);
                Assert.That(startedMicroreactor, Is.True);
                Assert.That(materials.GetMaterialAmount(context.Lathe, ProcessedMaterial), Is.Zero);
                Assert.That(materials.GetMaterialAmount(context.Lathe, "Uranium"), Is.Zero);
                Assert.That(materials.GetMaterialAmount(context.Lathe, "Steel"), Is.Zero);
                Assert.That(materials.GetMaterialAmount(context.Lathe, "Glass"), Is.Zero);
                Assert.That(materials.GetMaterialAmount(context.Lathe, "Gold"), Is.Zero);
                Assert.That(FindOutputs(context, "PowerCellMicroreactorPrinted"), Has.Count.EqualTo(1));
            });
        });
    }

    private static bool InsertMaterial(LatheContext context, string material, int amount)
    {
        var server = context.Pair.Server;
        var materials = server.System<MaterialStorageSystem>();
        var coordinates = server.EntMan.GetComponent<TransformComponent>(context.Lathe).Coordinates;
        var sheets = materials.SpawnMultipleFromMaterial(amount, material, coordinates);
        var inserted = sheets.Count != 0;
        foreach (var sheet in sheets)
            inserted &= materials.TryInsertMaterialEntity(context.User, sheet, context.Lathe);
        return inserted;
    }

    private static List<EntityUid> FindOutputs(LatheContext context, string prototype)
    {
        var results = new List<EntityUid>();
        var query = context.Pair.Server.EntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        var map = context.Pair.Server.EntMan.GetComponent<TransformComponent>(context.Lathe).MapUid;
        while (query.MoveNext(out var uid, out var metadata, out var transform))
        {
            if (metadata.EntityPrototype?.ID == prototype && transform.MapUid == map)
                results.Add(uid);
        }
        return results;
    }

    private static void AssertProcessedComposition(LatheContext context, EntityUid sheet)
    {
        var composition = context.Pair.Server.EntMan.GetComponent<PhysicalCompositionComponent>(sheet);
        Assert.That(composition.MaterialComposition, Has.Count.EqualTo(1));
        Assert.That(composition.MaterialComposition.GetValueOrDefault(ProcessedMaterial), Is.EqualTo(100));
        Assert.That(composition.MaterialComposition.ContainsKey("Uranium"), Is.False);
    }

    private static async Task WithLathe(string prototype, Func<LatheContext, Task> scenario)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid lathe = default;
        EntityUid researchServer = default;
        EntityUid user = default;
        var completed = false;
        try
        {
            await server.WaitPost(() =>
            {
                researchServer = server.EntMan.SpawnEntity("ResearchAndDevelopmentServer", map.GridCoords);
                lathe = server.EntMan.SpawnEntity(prototype, map.GridCoords);
                user = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
                server.System<PowerReceiverSystem>().SetNeedsPower(lathe, false);
                server.System<ServerResearchSystem>().RegisterClient(lathe, researchServer);
            });
            await server.WaitRunTicks(1);
            await server.WaitAssertion(() => Assert.That(server.System<PowerReceiverSystem>().IsPowered(lathe), Is.True));
            await scenario(new LatheContext(pair, lathe, researchServer, user));
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

    private sealed record LatheContext(TestPair Pair, EntityUid Lathe, EntityUid ResearchServer, EntityUid User);
}
