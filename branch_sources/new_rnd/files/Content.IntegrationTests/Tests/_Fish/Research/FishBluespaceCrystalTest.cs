#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server.Lathe;
using Content.Server.Materials;
using Content.Server.Parallax;
using Content.Server.Power.EntitySystems;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.IconSmoothing;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Materials.OreSilo;
using Content.Shared.Mining.Components;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Layers;
using Content.Shared.Research.Prototypes;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Noise;
using Robust.Shared.Prototypes;
using Robust.Shared.Reflection;
using Robust.Shared.Serialization.Manager;
using ServerResearchSystem = Content.Server.Research.Systems.ResearchSystem;

namespace Content.IntegrationTests.Tests._Fish.Research;

[TestFixture, NonParallelizable]
public sealed class FishBluespaceCrystalTest
{
    private const string CrystalMaterial = "FishBluespaceCrystal";
    private const string CrystalEntity = "FishBluespaceCrystal1";
    private static readonly ProtoId<LatheRecipePrototype> HoldingBagRecipe = "ClothingBackpackHolding";
    private static readonly ProtoId<DamageTypePrototype> StructuralDamage = "Structural";

    /// <summary>A researched holding bag cannot consume its other ingredients while its crystal is missing.</summary>
    [Test]
    public async Task HoldingBagRequiresCrystalBeforeReservingOtherMaterials()
    {
        await WithLathe("Protolathe", async context =>
        {
            var server = context.Pair.Server;
            var lathe = server.System<LatheSystem>();
            var materials = server.System<MaterialStorageSystem>();
            var inserted = true;
            Dictionary<ProtoId<MaterialPrototype>, int> stored = [];
            await server.WaitPost(() =>
            {
                server.System<ServerResearchSystem>().AddTechnology(context.ResearchServer, "BluespaceStorage");
                var recipe = server.ProtoMan.Index(HoldingBagRecipe);
                foreach (var (material, amount) in recipe.Materials)
                {
                    if (material.Id == CrystalMaterial)
                        continue;

                    var sheets = (int) Math.Ceiling(amount / 100d);
                    inserted &= InsertMaterial(context, material, sheets * 100);
                    stored[material] = materials.GetMaterialAmount(context.Lathe, material);
                }
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(inserted, Is.True);
                Assert.That(stored, Is.Not.Empty);
                var component = server.EntMan.GetComponent<LatheComponent>(context.Lathe);
                Assert.That(lathe.GetAvailableRecipes(context.Lathe, component), Does.Contain(HoldingBagRecipe));
                foreach (var (material, amount) in server.ProtoMan.Index(HoldingBagRecipe).Materials)
                {
                    if (material.Id != CrystalMaterial)
                        Assert.That(stored[material], Is.GreaterThanOrEqualTo(amount), material.Id);
                }
            });

            var canProduce = false;
            var queued = false;
            await server.WaitPost(() =>
            {
                var recipe = server.ProtoMan.Index(HoldingBagRecipe);
                canProduce = lathe.CanProduce(context.Lathe, recipe);
                queued = lathe.TryAddToQueue(context.Lathe, recipe, 1);
            });
            await server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(canProduce, Is.False, "Bluespace equipment requires a physical crystal.");
                    Assert.That(queued, Is.False);
                    foreach (var (material, amount) in stored)
                        Assert.That(materials.GetMaterialAmount(context.Lathe, material), Is.EqualTo(amount), material.Id);
                });
            });
        });
    }

    /// <summary>Every affected recipe requires its full crystal count and still consumes its other ingredients.</summary>
    [TestCase("ClothingBackpackHolding", 200)]
    [TestCase("ClothingBackpackSatchelHolding", 200)]
    [TestCase("ClothingBackpackDuffelHolding", 200)]
    [TestCase("PlantBagOfHolding", 200)]
    [TestCase("OreBagOfHolding", 200)]
    [TestCase("TrashBagHolding", 200)]
    [TestCase("BluespaceAirTank", 200)]
    [TestCase("JugBluespace", 200)]
    [TestCase("DeviceDesynchronizer", 200)]
    [TestCase("StationTeleporterMachineCircuitboard", 200)]
    [TestCase("CargoTelepadMachineCircuitboard", 200)]
    [TestCase("BorgModuleAdvancedMining", 200)]
    [TestCase("SmallBluespaceAirTank", 100)]
    [TestCase("BluespaceBeaker", 100)]
    [TestCase("SyringeBluespace", 100)]
    [TestCase("VialBluespace", 100)]
    [TestCase("BluespacePillPatchCanister", 100)]
    [TestCase("FishPouchMedicalBluespace", 100)]
    [TestCase("BluespaceBox", 100)]
    [TestCase("FireExtinguisherBluespace", 100)]
    [TestCase("DeviceQuantumSpinInverter", 100)]
    [TestCase("TeleporterChipBlank", 100)]
    [TestCase("StationTeleportersControlCircuitboard", 100)]
    [TestCase("BorgModuleAdvancedChemical", 100)]
    public async Task EveryBluespaceRecipeReservesExactlyItsCrystalCost(string recipeId, int crystalCost)
    {
        ProtoId<LatheRecipePrototype> recipePrototype = recipeId;
        await WithLathe(null, async context =>
        {
            var server = context.Pair.Server;
            var lathe = server.System<LatheSystem>();
            var materials = server.System<MaterialStorageSystem>();
            var inserted = true;
            var missingCrystalAccepted = false;
            var missingCrystalQueued = false;
            Dictionary<ProtoId<MaterialPrototype>, int> stored = [];
            await server.WaitPost(() =>
            {
                UnlockRecipe(context, recipePrototype);
                var recipe = server.ProtoMan.Index(recipePrototype);
                foreach (var (material, amount) in recipe.Materials)
                {
                    if (material.Id == CrystalMaterial)
                        continue;

                    inserted &= InsertMaterial(context, material, (int) Math.Ceiling(amount / 100d) * 100);
                    stored[material] = materials.GetMaterialAmount(context.Lathe, material);
                }
                if (crystalCost > 100)
                    inserted &= InsertMaterial(context, CrystalMaterial, crystalCost - 100);
                missingCrystalAccepted = lathe.CanProduce(context.Lathe, recipe);
                missingCrystalQueued = lathe.TryAddToQueue(context.Lathe, recipe, 1);
            });
            await server.WaitAssertion(() =>
            {
                var recipe = server.ProtoMan.Index(recipePrototype);
                Assert.That(inserted, Is.True);
                Assert.That(recipe.Materials.GetValueOrDefault(CrystalMaterial), Is.EqualTo(crystalCost));
                Assert.That(stored, Is.Not.Empty, "Adding a crystal must preserve the original material costs.");
                AssertInheritedIngredients(recipe);
                Assert.That(lathe.GetAvailableRecipes(context.Lathe, server.EntMan.GetComponent<LatheComponent>(context.Lathe)),
                    Does.Contain(recipePrototype));
                Assert.That(missingCrystalAccepted, Is.False);
                Assert.That(missingCrystalQueued, Is.False);
                foreach (var (material, amount) in stored)
                    Assert.That(materials.GetMaterialAmount(context.Lathe, material), Is.EqualTo(amount), material.Id);
            });

            var canProduce = false;
            var queued = false;
            var started = false;
            await server.WaitPost(() =>
            {
                inserted = InsertMaterial(context, CrystalMaterial, 100);
                var recipe = server.ProtoMan.Index(recipePrototype);
                canProduce = lathe.CanProduce(context.Lathe, recipe);
                queued = lathe.TryAddToQueue(context.Lathe, recipe, 1);
                started = lathe.TryStartProducing(context.Lathe);
                if (started)
                    lathe.FinishProducing(context.Lathe);
            });
            await server.WaitAssertion(() =>
            {
                var recipe = server.ProtoMan.Index(recipePrototype);
                Assert.That(inserted, Is.True);
                Assert.That(canProduce, Is.True);
                Assert.That(queued, Is.True);
                Assert.That(started, Is.True);
                Assert.That(materials.GetMaterialAmount(context.Lathe, CrystalMaterial), Is.Zero);
                foreach (var (material, amount) in stored)
                    Assert.That(materials.GetMaterialAmount(context.Lathe, material), Is.EqualTo(amount - recipe.Materials[material]), material.Id);
                Assert.That(recipe.Result, Is.Not.Null);
                Assert.That(FindOutputs(context, recipe.Result!.Value.Id), Has.Count.EqualTo(1));
            });
        }, recipePrototype);
    }

    /// <summary>Fractional hyperlathe discounts preserve whole crystals, ordinary discounts, and exact cancellation refunds.</summary>
    [TestCase("BluespaceBeaker", 100, 0.75f, 100)]
    [TestCase("ClothingBackpackHolding", 200, 0.75f, 200)]
    [TestCase("ClothingBackpackHolding", 200, 0.5f, 100)]
    public async Task HyperlatheDiscountAndRefundKeepCrystalsWhole(string recipeId, int crystalCost, float multiplier, int effectiveCrystalCost)
    {
        ProtoId<LatheRecipePrototype> recipePrototype = recipeId;
        await WithLathe("ProtolatheHyperConvection", async context =>
        {
            var server = context.Pair.Server;
            var lathe = server.System<LatheSystem>();
            var materials = server.System<MaterialStorageSystem>();
            var inserted = true;
            var queued = false;
            var started = false;
            var nativeMultiplier = 0f;
            Dictionary<ProtoId<MaterialPrototype>, int> stored = [];
            await server.WaitPost(() =>
            {
                UnlockRecipe(context, recipePrototype);
                var component = server.EntMan.GetComponent<LatheComponent>(context.Lathe);
                nativeMultiplier = component.MaterialUseMultiplier;
                component.MaterialUseMultiplier = multiplier;
                var recipe = server.ProtoMan.Index(recipePrototype);
                foreach (var (material, amount) in recipe.Materials)
                {
                    inserted &= InsertMaterial(context, material, (int) Math.Ceiling(amount * 2 / 100d) * 100);
                    stored[material] = materials.GetMaterialAmount(context.Lathe, material);
                }
                queued = lathe.TryAddToQueue(context.Lathe, recipe, 2);
            });
            await server.WaitAssertion(() =>
            {
                var recipe = server.ProtoMan.Index(recipePrototype);
                ProtoId<MaterialPrototype> crystal = CrystalMaterial;
                Assert.That(inserted, Is.True);
                Assert.That(queued, Is.True);
                if (multiplier == 0.5f)
                    Assert.That(nativeMultiplier, Is.EqualTo(0.5f));
                Assert.That(SharedLatheSystem.AdjustMaterial(server.ProtoMan.Index(crystal), crystalCost, true, multiplier),
                    Is.EqualTo(effectiveCrystalCost), "Displayed cost must agree with the server reservation.");
                Assert.That(materials.GetMaterialAmount(context.Lathe, CrystalMaterial), Is.EqualTo((crystalCost - effectiveCrystalCost) * 2));
                foreach (var (material, amount) in stored)
                {
                    if (material.Id == CrystalMaterial)
                        continue;
                    var discounted = (int) (recipe.Materials[material] * multiplier);
                    Assert.That(discounted, Is.LessThan(recipe.Materials[material]));
                    Assert.That(materials.GetMaterialAmount(context.Lathe, material), Is.EqualTo(amount - discounted * 2), material.Id);
                }
            });
            await server.WaitPost(() => DeleteFirstBatch(context));
            await server.WaitAssertion(() => AssertStoredMaterials(context, stored));

            await server.WaitPost(() =>
            {
                queued = lathe.TryAddToQueue(context.Lathe, server.ProtoMan.Index(recipePrototype), 2);
                started = lathe.TryStartProducing(context.Lathe);
                var abort = new LatheAbortFabricationMessage { Actor = context.User };
                var component = server.EntMan.GetComponent<LatheComponent>(context.Lathe);
                lathe.OnLatheAbortFabricationMessage(context.Lathe, component, ref abort);
                lathe.OnLatheAbortFabricationMessage(context.Lathe, component, ref abort);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(queued, Is.True);
                Assert.That(started, Is.True);
                AssertStoredMaterials(context, stored);
                Assert.That(server.EntMan.GetComponent<LatheComponent>(context.Lathe).Queue, Is.Empty);
                Assert.That(FindOutputs(context, server.ProtoMan.Index(recipePrototype).Result!.Value.Id), Is.Empty);
            });
        });
    }

    /// <summary>Restoring power prints each paid item exactly once, including the last item of a batch.</summary>
    [TestCase("BluespaceBeaker", 1)]
    [TestCase("ClothingBackpackHolding", 2)]
    public async Task PowerInterruptionPreservesPaidCrystalReservation(string recipeId, int quantity)
    {
        ProtoId<LatheRecipePrototype> recipePrototype = recipeId;
        await WithLathe("Protolathe", async context =>
        {
            var server = context.Pair.Server;
            var lathe = server.System<LatheSystem>();
            var materials = server.System<MaterialStorageSystem>();
            var power = server.System<PowerReceiverSystem>();
            var inserted = true;
            var queued = false;
            var started = false;
            Dictionary<ProtoId<MaterialPrototype>, int> stored = [];
            await server.WaitPost(() =>
            {
                UnlockRecipe(context, recipePrototype);
                var recipe = server.ProtoMan.Index(recipePrototype);
                foreach (var (material, amount) in recipe.Materials)
                {
                    inserted &= InsertMaterial(context, material, (int) Math.Ceiling(amount * quantity / 100d) * 100);
                    stored[material] = materials.GetMaterialAmount(context.Lathe, material);
                }
                queued = lathe.TryAddToQueue(context.Lathe, recipe, quantity);
                started = lathe.TryStartProducing(context.Lathe);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(inserted, Is.True);
                Assert.That(queued, Is.True);
                Assert.That(started, Is.True);
                Assert.That(materials.GetMaterialAmount(context.Lathe, CrystalMaterial), Is.Zero);
            });
            await server.WaitPost(() => power.SetNeedsPower(context.Lathe, true));
            await server.WaitRunTicks(1);
            await server.WaitAssertion(() => Assert.That(power.IsPowered(context.Lathe), Is.False));
            await server.WaitPost(() => power.SetNeedsPower(context.Lathe, false));
            await server.WaitRunTicks(1);
            await server.WaitAssertion(() => Assert.That(power.IsPowered(context.Lathe), Is.True));
            await server.WaitPost(() =>
            {
                for (var index = 0; index < quantity; index++)
                    lathe.FinishProducing(context.Lathe);
            });
            await server.WaitAssertion(() =>
            {
                var recipe = server.ProtoMan.Index(recipePrototype);
                Assert.Multiple(() =>
                {
                    Assert.That(FindOutputs(context, recipe.Result!.Value.Id), Has.Count.EqualTo(quantity));
                    foreach (var (material, amount) in stored)
                        Assert.That(materials.GetMaterialAmount(context.Lathe, material),
                            Is.EqualTo(amount - recipe.Materials[material] * quantity), material.Id);
                    Assert.That(server.EntMan.GetComponent<LatheComponent>(context.Lathe).Queue, Is.Empty);
                    Assert.That(lathe.CanProduce(context.Lathe, recipe), Is.False, "A power cycle cannot create a free extra crystal.");
                });
            });
        });
    }

    /// <summary>Mining any supported rock variant yields exactly one usable crystal.</summary>
    [TestCase("FishAsteroidRockBluespaceCrystal")]
    [TestCase("FishIronRockBluespaceCrystal")]
    [TestCase("FishWallRockBluespaceCrystal")]
    [TestCase("FishWallRockSnowBluespaceCrystal")]
    [TestCase("FishWallRockSandBluespaceCrystal")]
    [TestCase("FishWallRockChromiteBluespaceCrystal")]
    [TestCase("FishWallRockBasaltBluespaceCrystal")]
    public async Task MiningRockDropsExactlyOneCrystal(string rockPrototype)
    {
        await WithLathe("Protolathe", async context =>
        {
            var server = context.Pair.Server;
            EntityUid rock = default;
            var damaged = false;
            await server.WaitPost(() =>
            {
                var coordinates = server.EntMan.GetComponent<TransformComponent>(context.Lathe).Coordinates;
                rock = server.EntMan.SpawnEntity(rockPrototype, coordinates);
                var damage = new DamageSpecifier(server.ProtoMan.Index(StructuralDamage), 1000);
                damaged = server.System<DamageableSystem>().TryChangeDamage(rock, damage,
                    ignoreResistances: true, ignoreGlobalModifiers: true, ignoreVariance: true);
            });
            await server.WaitRunTicks(1);
            await server.WaitAssertion(() =>
            {
                Assert.That(damaged, Is.True);
                Assert.That(server.EntMan.EntityExists(rock), Is.False);
                var crystals = FindOutputs(context, CrystalEntity);
                Assert.That(crystals.Sum(uid => server.EntMan.GetComponent<StackComponent>(uid).Count), Is.EqualTo(1));
                foreach (var crystal in crystals)
                    AssertCrystalComposition(context, crystal);
            });
        });
    }

    /// <summary>A mined crystal fits an ore bag and survives lathe and silo ejection before paying for equipment.</summary>
    [Test]
    public async Task CrystalMovesThroughOreBagLatheAndSiloWithoutGrowth()
    {
        await WithLathe("Protolathe", async context =>
        {
            var server = context.Pair.Server;
            var materials = server.System<MaterialStorageSystem>();
            var lathe = server.System<LatheSystem>();
            EntityUid bag = default;
            EntityUid crystal = default;
            EntityUid silo = default;
            var bagInserted = false;
            var removed = false;
            var inserted = false;
            List<EntityUid> ejected = [];
            ProtoId<LatheRecipePrototype> recipePrototype = "BluespaceBeaker";
            await server.WaitPost(() =>
            {
                var coordinates = server.EntMan.GetComponent<TransformComponent>(context.Lathe).Coordinates;
                bag = server.EntMan.SpawnEntity("OreBag", coordinates);
                crystal = server.EntMan.SpawnEntity(CrystalEntity, coordinates);
                silo = server.EntMan.SpawnEntity("MachineMaterialSilo", coordinates);
                server.System<PowerReceiverSystem>().SetNeedsPower(silo, false);
                UnlockRecipe(context, recipePrototype);
                bagInserted = server.System<StorageSystem>().Insert(bag, crystal, out _, user: context.User);
            });
            await server.WaitRunTicks(1);
            await server.WaitAssertion(() =>
            {
                Assert.That(bagInserted, Is.True);
                Assert.That(server.EntMan.GetComponent<StorageComponent>(bag).Container.Contains(crystal), Is.True);
                Assert.That(server.System<PowerReceiverSystem>().IsPowered(silo), Is.True);
                AssertCrystalComposition(context, crystal);
            });
            await server.WaitPost(() =>
            {
                removed = server.System<SharedContainerSystem>().TryRemoveFromContainer(crystal);
                inserted = materials.TryInsertMaterialEntity(context.User, crystal, context.Lathe);
                ejected = materials.EjectMaterial(context.Lathe, CrystalMaterial);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(removed, Is.True);
                Assert.That(inserted, Is.True);
                Assert.That(materials.GetMaterialAmount(context.Lathe, CrystalMaterial), Is.Zero);
                Assert.That(ejected.Sum(uid => server.EntMan.GetComponent<StackComponent>(uid).Count), Is.EqualTo(1));
                foreach (var output in ejected)
                    AssertCrystalComposition(context, output);
            });
            await server.WaitPost(() =>
            {
                inserted = true;
                foreach (var output in ejected)
                    inserted &= materials.TryInsertMaterialEntity(context.User, output, silo);
                var link = new ToggleOreSiloClientMessage(server.EntMan.GetNetEntity(context.Lathe)) { Actor = context.User };
                server.EntMan.EventBus.RaiseLocalEvent(silo, link);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(inserted, Is.True);
                Assert.That(server.EntMan.GetComponent<OreSiloClientComponent>(context.Lathe).Silo, Is.EqualTo(silo));
                Assert.That(materials.GetMaterialAmount(silo, CrystalMaterial), Is.EqualTo(100));
                Assert.That(materials.GetMaterialAmount(context.Lathe, CrystalMaterial), Is.EqualTo(100));
                Assert.That(materials.GetMaterialAmount(context.Lathe, CrystalMaterial, localOnly: true), Is.Zero);
            });
            await server.WaitPost(() => ejected = materials.EjectMaterial(context.Lathe, CrystalMaterial));
            await server.WaitAssertion(() =>
            {
                Assert.That(materials.GetMaterialAmount(silo, CrystalMaterial), Is.Zero);
                Assert.That(ejected.Sum(uid => server.EntMan.GetComponent<StackComponent>(uid).Count), Is.EqualTo(1));
                foreach (var output in ejected)
                    AssertCrystalComposition(context, output);
            });
            var queued = false;
            var started = false;
            await server.WaitPost(() =>
            {
                inserted = true;
                foreach (var output in ejected)
                    inserted &= materials.TryInsertMaterialEntity(context.User, output, silo);
                var recipe = server.ProtoMan.Index(recipePrototype);
                foreach (var (material, amount) in recipe.Materials)
                {
                    if (material.Id != CrystalMaterial)
                        inserted &= InsertMaterial(context, material, (int) Math.Ceiling(amount / 100d) * 100);
                }
                queued = lathe.TryAddToQueue(context.Lathe, recipe, 1);
                started = lathe.TryStartProducing(context.Lathe);
                if (started)
                    lathe.FinishProducing(context.Lathe);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(inserted, Is.True);
                Assert.That(queued, Is.True);
                Assert.That(started, Is.True);
                Assert.That(materials.GetMaterialAmount(silo, CrystalMaterial), Is.Zero);
                Assert.That(materials.GetMaterialAmount(context.Lathe, CrystalMaterial), Is.Zero);
                Assert.That(FindOutputs(context, "BluespaceBeaker"), Has.Count.EqualTo(1));
            });
        });
    }

    /// <summary>The real planet noise produces a nonzero, sparse crystal layer across several seeds and distant regions.</summary>
    [TestCase("TerraPlanet", "FishWallRockBluespaceCrystal")]
    [TestCase("GlacierPlanet", "FishWallRockSnowBluespaceCrystal")]
    public async Task PlanetCrystalNoiseIsSparseAndSelectable(string biomeId, string expectedEntity)
    {
        await WithLathe("Protolathe", async context =>
        {
            var server = context.Pair.Server;
            ProtoId<BiomeTemplatePrototype> templateId = biomeId;
            var counts = new List<int>();
            var referenceCounts = new Dictionary<string, List<int>>();
            var diagnosticThresholds = new[] { 0.97f, 0.98f, 0.985f, 0.99f, 0.995f };
            var diagnosticCounts = diagnosticThresholds.ToDictionary(threshold => threshold, _ => new List<int>());
            var selected = new List<string?>();
            await server.WaitPost(() =>
            {
                var template = server.ProtoMan.Index(templateId);
                var layer = template.Layers.OfType<BiomeEntityLayer>()
                    .Single(candidate => candidate.Entities.Any(entity => entity.Id == expectedEntity));
                var serialization = server.ResolveDependency<ISerializationManager>();
                var tileDefinitions = server.ResolveDependency<ITileDefinitionManager>();
                var tile = new Tile(tileDefinitions[layer.AllowedTiles.Single().Id].TileId);
                var seeds = new[] { 0, 12345, -789012 };
                var offsets = new[] { new Vector2i(0, 0), new Vector2i(4096, -2048), new Vector2i(-8192, 16384) };
                for (var sample = 0; sample < seeds.Length; sample++)
                {
                    var noise = new FastNoiseLite();
                    serialization.CopyTo(layer.Noise, ref noise, notNullableOverride: true);
                    noise.SetSeed(noise.GetSeed() + seeds[sample]);
                    noise.SetFractalOctaves(noise.GetFractalOctaves());
                    var hits = 0;
                    var diagnosticHits = new int[diagnosticThresholds.Length];
                    for (var x = 0; x < 256; x++)
                    for (var y = 0; y < 256; y++)
                    {
                        var position = offsets[sample] + new Vector2i(x, y);
                        var value = noise.GetNoise(position.X, position.Y);
                        var adjusted = layer.Invert ? -value : value;
                        for (var index = 0; index < diagnosticThresholds.Length; index++)
                        {
                            if (adjusted >= diagnosticThresholds[index])
                                diagnosticHits[index]++;
                        }
                        if (adjusted < layer.Threshold)
                            continue;
                        hits++;
                        if (hits <= 3)
                        {
                            server.System<BiomeSystem>().TryGetEntity(position, template.Layers, tile, seeds[sample],
                                (Entity<MapGridComponent>?) null, out var entity);
                            selected.Add(entity);
                        }
                    }
                    counts.Add(hits);
                    for (var index = 0; index < diagnosticThresholds.Length; index++)
                        diagnosticCounts[diagnosticThresholds[index]].Add(diagnosticHits[index]);
                }
                foreach (var reference in template.Layers.OfType<BiomeEntityLayer>().Where(candidate =>
                             candidate.Entities.Any(entity => entity.Id.EndsWith("Diamond") || entity.Id.EndsWith("Uranium"))))
                {
                    var referenceName = string.Join(",", reference.Entities);
                    List<int> referenceHits = [];
                    for (var sample = 0; sample < seeds.Length; sample++)
                    {
                        var noise = new FastNoiseLite();
                        serialization.CopyTo(reference.Noise, ref noise, notNullableOverride: true);
                        noise.SetSeed(noise.GetSeed() + seeds[sample]);
                        noise.SetFractalOctaves(noise.GetFractalOctaves());
                        var hits = 0;
                        for (var x = 0; x < 256; x++)
                        for (var y = 0; y < 256; y++)
                        {
                            var position = offsets[sample] + new Vector2i(x, y);
                            var value = noise.GetNoise(position.X, position.Y);
                            if ((reference.Invert ? -value : value) >= reference.Threshold)
                                hits++;
                        }
                        referenceHits.Add(hits);
                    }
                    referenceCounts.Add(referenceName, referenceHits);
                }
            });
            TestContext.Out.WriteLine($"{biomeId}: 256x256 hits per seed/region = {string.Join(", ", counts)}; " +
                                      $"density = {counts.Sum() / (3d * 256 * 256):P4}");
            foreach (var (reference, hits) in referenceCounts)
                TestContext.Out.WriteLine($"{reference}: hits = {string.Join(", ", hits)}; density = {hits.Sum() / (3d * 256 * 256):P4}");
            foreach (var (threshold, hits) in diagnosticCounts)
                TestContext.Out.WriteLine($"{biomeId} threshold {threshold}: hits = {string.Join(", ", hits)}; density = {hits.Sum() / (3d * 256 * 256):P4}");
            await server.WaitAssertion(() =>
            {
                Assert.That(counts.Sum(), Is.GreaterThan(0), "The configured rare layer must actually be reachable.");
                Assert.That(counts.Sum(), Is.LessThan(3 * 256 * 256 / 100), "Crystal noise must occupy less than one percent of eligible tiles.");
                Assert.That(referenceCounts, Is.Not.Empty);
                foreach (var (reference, hits) in referenceCounts)
                    Assert.That(counts.Sum(), Is.LessThan(hits.Sum()), $"Crystal ore must be rarer than {reference} on the same samples.");
                Assert.That(selected, Is.Not.Empty);
                Assert.That(selected, Is.All.EqualTo(expectedEntity), "The complete biome layer stack must select the crystal at sampled hits.");
            });
        });
    }

    /// <summary>All rock variants retain a visible, renderable ore overlay after live client smoothing.</summary>
    [Test]
    public async Task NetworkedRockSpritesRetainOreOverlayAfterSmoothing()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var session = pair.Player!;
        var previousEntity = session.AttachedEntity;
        var rocks = new Dictionary<string, NetEntity>();
        var completed = false;
        try
        {
            await server.WaitPost(() =>
            {
                var user = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
                server.PlayerMan.SetAttachedEntity(session, user);
                var prototypes = new[]
                {
                    "FishAsteroidRockBluespaceCrystal", "FishIronRockBluespaceCrystal", "FishWallRockBluespaceCrystal",
                    "FishWallRockSnowBluespaceCrystal", "FishWallRockSandBluespaceCrystal",
                    "FishWallRockChromiteBluespaceCrystal", "FishWallRockBasaltBluespaceCrystal",
                };
                for (var index = 0; index < prototypes.Length; index++)
                {
                    var rock = server.EntMan.SpawnEntity(prototypes[index], map.GridCoords.Offset(new Vector2(index * 2 - 6, 2)));
                    rocks.Add(prototypes[index], server.EntMan.GetNetEntity(rock));
                }
            });
            await pair.RunTicksSync(15);
            await pair.SyncTicks(targetDelta: 1);
            await client.WaitAssertion(() =>
            {
                var sprites = client.System<SpriteSystem>();
                var reflection = client.ResolveDependency<IReflectionManager>();
                foreach (var (prototype, networkEntity) in rocks)
                {
                    var rock = client.EntMan.GetEntity(networkEntity);
                    Assert.That(client.EntMan.EntityExists(rock), Is.True, prototype);
                    var sprite = client.EntMan.GetComponent<SpriteComponent>(rock);
                    var smoothEdges = client.EntMan.HasComponent<SmoothEdgeComponent>(rock);
                    Assert.That(sprite.Visible, Is.True, prototype);
                    Assert.That(sprite.AllLayers.Count(), Is.GreaterThanOrEqualTo(smoothEdges ? 6 : 5), prototype);
                    Assert.That(sprites.TryGetLayer((rock, sprite), MiningScannerVisualLayers.Overlay, out var overlay, false), Is.True, prototype);
                    Assert.That(overlay!.Visible, Is.True, prototype);
                    var overlayState = ((ISpriteLayer) overlay).RsiState;
                    Assert.That(overlayState.Name, Is.EqualTo("rock_bluespace_crystal"), prototype);
                    Assert.That(overlay.ActualRsi, Is.Not.Null, prototype);
                    Assert.That(overlay.ActualRsi!.TryGetState(overlayState, out _), Is.True, prototype);
                    if (!smoothEdges)
                        continue;
                    foreach (var direction in new[] { "South", "East", "North", "West" })
                    {
                        Assert.That(reflection.TryParseEnumReference($"enum.EdgeLayer.{direction}", out var key), Is.True);
                        Assert.That(sprites.LayerMapTryGet((rock, sprite), key!, out _, false), Is.True, $"{prototype}: {direction}");
                    }
                }
            });
            completed = true;
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.PlayerMan.SetAttachedEntity(session, previousEntity);
                server.EntMan.DeleteEntity(map.MapUid);
            });
            await pair.RunTicksSync(5);
            if (completed)
                await pair.CleanReturnAsync();
            else
                await pair.DisposeAsync();
        }
    }

    private static void AssertCrystalComposition(LatheContext context, EntityUid crystal)
    {
        var server = context.Pair.Server;
        Assert.That(server.EntMan.GetComponent<StackComponent>(crystal).StackTypeId.Id, Is.EqualTo(CrystalMaterial));
        var composition = server.EntMan.GetComponent<PhysicalCompositionComponent>(crystal).MaterialComposition;
        Assert.That(composition, Has.Count.EqualTo(1));
        Assert.That(composition.GetValueOrDefault(CrystalMaterial), Is.EqualTo(100));
    }

    private static void DeleteFirstBatch(LatheContext context)
    {
        var server = context.Pair.Server;
        var message = new LatheDeleteRequestMessage(0) { Actor = context.User };
        server.System<LatheSystem>().OnLatheDeleteRequestMessage(context.Lathe,
            server.EntMan.GetComponent<LatheComponent>(context.Lathe), ref message);
    }

    private static void AssertStoredMaterials(LatheContext context, Dictionary<ProtoId<MaterialPrototype>, int> expected)
    {
        var materials = context.Pair.Server.System<MaterialStorageSystem>();
        foreach (var (material, amount) in expected)
            Assert.That(materials.GetMaterialAmount(context.Lathe, material), Is.EqualTo(amount), material.Id);
    }

    private static void AssertInheritedIngredients(LatheRecipePrototype recipe)
    {
        Dictionary<ProtoId<MaterialPrototype>, int>? expected = recipe.ID switch
        {
            "StationTeleporterMachineCircuitboard" or "CargoTelepadMachineCircuitboard" or "StationTeleportersControlCircuitboard" =>
                new() { ["Steel"] = 100, ["Glass"] = 500, ["Gold"] = 100 },
            "BorgModuleAdvancedMining" or "BorgModuleAdvancedChemical" =>
                new() { ["Steel"] = 500, ["Glass"] = 500, ["Plastic"] = 250, ["Gold"] = 50 },
            "ClothingBackpackHolding" or "ClothingBackpackSatchelHolding" or "ClothingBackpackDuffelHolding" or "PlantBagOfHolding" or "OreBagOfHolding" =>
                new() { ["Steel"] = 2000, ["Silver"] = 750, ["Plasma"] = 1500, ["Uranium"] = 150 },
            _ => null,
        };
        if (expected == null)
            return;
        foreach (var (material, amount) in expected)
            Assert.That(recipe.Materials.GetValueOrDefault(material), Is.EqualTo(amount), $"{recipe.ID}: {material}");
        Assert.That(recipe.Materials, Has.Count.EqualTo(expected.Count + 1));
    }

    private static void UnlockRecipe(LatheContext context, ProtoId<LatheRecipePrototype> recipe)
    {
        var server = context.Pair.Server;
        foreach (var technology in server.ProtoMan.EnumeratePrototypes<TechnologyPrototype>())
        {
            if (technology.RecipeUnlocks.Contains(recipe))
                server.System<ServerResearchSystem>().AddTechnology(context.ResearchServer, technology.ID);
        }
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

    private static bool InsertMaterial(LatheContext context, ProtoId<MaterialPrototype> material, int amount)
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

    private static async Task WithLathe(string? prototype, Func<LatheContext, Task> scenario, ProtoId<LatheRecipePrototype>? recipe = null)
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
                if (prototype == null)
                {
                    var candidates = new[] { "Protolathe", "MedicalTechFab", "ExosuitFabricator", "CircuitImprinter" };
                    prototype = pair.GetPrototypesWithComponent<LatheComponent>()
                        .Where(entry => candidates.Contains(entry.Item1.ID))
                        .First(entry => server.System<LatheSystem>().GetAllPossibleRecipes(entry.Item2).Contains(recipe!.Value)).Item1.ID;
                }
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
