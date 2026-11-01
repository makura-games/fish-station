using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Server._Fish.Xenobiology;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._Fish.Xenobiology;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Temperature.Components;
using Content.Shared.Temperature.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.Xenobiology;

[TestFixture, NonParallelizable]
public sealed class FishXenobiologyLifecycleTest
{
    /// <summary>A moderately hungry specimen independently chooses and swallows food from the floor.</summary>
    [Test]
    public async Task NativeNpcFeedingWorksBetweenStarvingAndOkaySatiety()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            var originallyEnabled = false;
            EntityUid food = default;
            try
            {
                await server.WaitPost(() =>
                {
                    var npc = server.System<NPCSystem>();
                    originallyEnabled = npc.Enabled;
                    npc.Enabled = true;
                    food = server.EntMan.SpawnEntity("FoodMeat",
                        new EntityCoordinates(lab.Coordinates.EntityId, 2.75f, 1.5f));
                    server.System<SatiationSystem>().SetValue((lab.Slime,
                        server.EntMan.GetComponent<SatiationComponent>(lab.Slime)), SatiationSystem.Hunger, 40f);
                    server.System<HTNSystem>().SetHTNEnabled((lab.Slime,
                        server.EntMan.GetComponent<HTNComponent>(lab.Slime)), true);
                });
                await server.WaitAssertion(() =>
                {
                    var hunger = server.System<SatiationSystem>().GetValueOrNull((lab.Slime,
                        server.EntMan.GetComponent<SatiationComponent>(lab.Slime)), SatiationSystem.Hunger);
                    Assert.That(hunger, Is.InRange(39f, 41f));
                    Assert.That(lab.State.Biomass, Is.Zero);
                    Assert.That(server.System<SharedHandsSystem>().IsHolding(lab.User, food), Is.False);
                });

                // Fish-edit: ждём самостоятельного планирования, подхода к еде и штатного проглатывания.
                await lab.Pair.RunSeconds(10f);
                await server.WaitAssertion(() =>
                {
                    Assert.That(lab.State.Biomass, Is.GreaterThan(0),
                        "A specimen below Okay satiety must independently eat nearby food, even above the starvation threshold.");
                    Assert.That(lab.State.ProteinIntake, Is.GreaterThan(0));
                });
            }
            finally
            {
                await server.WaitPost(() => server.System<NPCSystem>().Enabled = originallyEnabled);
            }
        });
    }

    /// <summary>Starting or cancelling feeding never creates biomass; a real swallowed bite does.</summary>
    [Test]
    public async Task NativeFeedingCreditsOnlyASuccessfulSwallow()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            EntityUid food = default;
            var equipped = false;
            var started = false;
            await server.WaitPost(() =>
            {
                food = server.EntMan.SpawnEntity("FoodMeat", lab.Coordinates);
                equipped = server.System<SharedHandsSystem>().TryPickupAnyHand(lab.User, food);
                started = server.System<IngestionSystem>().TryIngest(lab.User, lab.Slime, food);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(equipped, Is.True);
                Assert.That(started, Is.True, "The laboratory body must support ordinary hand-feeding.");
                Assert.That(lab.State.Biomass, Is.Zero);
            });

            await lab.Pair.RunSeconds(0.5f);
            await server.WaitPost(() =>
            {
                foreach (var id in server.EntMan.GetComponent<DoAfterComponent>(lab.User).DoAfters.Keys.ToArray())
                    server.System<SharedDoAfterSystem>().Cancel(lab.User, id);
            });
            await lab.Pair.RunSeconds(3.5f);
            await server.WaitAssertion(() => Assert.That(lab.State.Biomass, Is.Zero,
                "Cancelled feeding must not count as consumed food."));

            await server.WaitPost(() => started = server.System<IngestionSystem>().TryIngest(lab.User, lab.Slime, food));
            await server.WaitAssertion(() => Assert.That(started, Is.True));
            // Fish-edit: штатное принудительное кормление занимает три секунды.
            await lab.Pair.RunSeconds(3.5f);
            await server.WaitAssertion(() =>
            {
                Assert.That(lab.State.Biomass, Is.GreaterThan(0));
                Assert.That(lab.State.ProteinIntake, Is.GreaterThan(0));
            });
        });
    }

    /// <summary>Actual heat changes gate maturation and division while adulthood preserves entity identity.</summary>
    [Test]
    public async Task TemperatureGatesMaturationAndDivision()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            var matureCold = true;
            var matureWarm = false;
            var divideCold = true;
            var divideWarm = false;
            await server.WaitPost(() =>
            {
                lab.State.Growth = 180f;
                lab.State.Biomass = 100f;
                lab.SetTemperature(270.15f);
                matureCold = server.System<FishXenobiologySystem>().TryMature(lab.Slime);
                lab.SetTemperature(293.15f);
                matureWarm = server.System<FishXenobiologySystem>().TryMature(lab.Slime);
                lab.SetTemperature(270.15f);
                divideCold = server.System<FishXenobiologySystem>().CanDivide(lab.Slime);
                lab.SetTemperature(293.15f);
                divideWarm = server.System<FishXenobiologySystem>().CanDivide(lab.Slime);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(matureCold, Is.False);
                Assert.That(matureWarm, Is.True);
                Assert.That(server.EntMan.EntityExists(lab.Slime), Is.True);
                Assert.That(lab.State.Adult, Is.True);
                Assert.That(lab.State.Biomass, Is.EqualTo(80f));
                Assert.That(divideCold, Is.False);
                Assert.That(divideWarm, Is.True);
            });
        });
    }

    /// <summary>One division transfers the parental traits to a finite litter and cannot be repeated.</summary>
    [Test]
    public async Task DivisionIsOneTimeAndInheritsStabilizedTraits()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            var first = false;
            var repeated = true;
            await server.WaitPost(() =>
            {
                lab.State.Adult = true;
                lab.State.Biomass = 100;
                lab.State.Generation = 4;
                lab.State.MutationChance = 0;
                lab.State.Offspring = 3;
                lab.State.ExtractYield = 3;
                lab.State.GrowthRate = 1.5f;
                lab.State.Docility = 0.75f;
                lab.State.ThermalTolerance = 10f;
                first = server.System<FishXenobiologySystem>().TryDivide(lab.Slime);
                repeated = server.System<FishXenobiologySystem>().TryDivide(lab.Slime);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(first, Is.True);
                Assert.That(repeated, Is.False);
                var children = lab.Specimens().Where(pair => pair.Uid != lab.Slime).ToArray();
                Assert.That(children, Has.Length.EqualTo(3));
                foreach (var (_, child) in children)
                {
                    Assert.That(child.Breed.Id, Is.EqualTo("FishGrey"));
                    Assert.That(child.Generation, Is.EqualTo(5));
                    Assert.That(child.MutationChance, Is.Zero);
                    Assert.That(child.Offspring, Is.EqualTo(3));
                    Assert.That(child.ExtractYield, Is.EqualTo(3));
                    Assert.That(child.GrowthRate, Is.InRange(1.425f, 1.575f));
                    Assert.That(child.Docility, Is.EqualTo(0.75f));
                    Assert.That(child.ThermalTolerance, Is.EqualTo(10f));
                    Assert.That(child.Adult, Is.False);
                    Assert.That(child.Biomass, Is.Zero);
                }
            });
        });
    }

    /// <summary>Guaranteed mutation follows the parent's declared outgoing edges.</summary>
    [Test]
    public async Task MutationUsesTheBreedTree()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var divided = false;
            await lab.Pair.Server.WaitPost(() =>
            {
                lab.State.Adult = true;
                lab.State.Biomass = 100;
                lab.State.MutationChance = 1;
                divided = lab.Pair.Server.System<FishXenobiologySystem>().TryDivide(lab.Slime);
            });
            await lab.Pair.Server.WaitAssertion(() =>
            {
                Assert.That(divided, Is.True);
                ProtoId<FishXenobiologyBreedPrototype> grey = "FishGrey";
                var breed = lab.Pair.Server.ResolveDependency<IPrototypeManager>().Index(grey);
                foreach (var (_, child) in lab.Specimens().Where(pair => pair.Uid != lab.Slime))
                {
                    Assert.That(breed.Mutations.ContainsKey(child.Breed), Is.True);
                    Assert.That(child.MutationChance, Is.EqualTo(1f));
                }
            });
        });
    }

    /// <summary>The population limit prevents an oversized litter before the adult is spent.</summary>
    [TestCase(false)]
    [TestCase(true)]
    public async Task PopulationLimitDoesNotConsumeTheParent(bool outsideGrid)
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var divided = true;
            await lab.Pair.Server.WaitPost(() =>
            {
                lab.State.Adult = true;
                lab.State.Biomass = 100;
                lab.State.Offspring = 3;
                for (var i = 0; i < 62; i++)
                {
                    var position = outsideGrid ? new EntityCoordinates(lab.Map, 20, 20) : lab.Coordinates;
                    var slime = lab.Pair.Server.EntMan.SpawnEntity("FishXenoSlimeGrey", position);
                    lab.Pair.Server.System<HTNSystem>().SetHTNEnabled(
                        (slime, lab.Pair.Server.EntMan.GetComponent<HTNComponent>(slime)), false);
                }
                divided = lab.Pair.Server.System<FishXenobiologySystem>().TryDivide(lab.Slime);
            });
            await lab.Pair.Server.WaitAssertion(() =>
            {
                Assert.That(divided, Is.False);
                Assert.That(lab.State.Consumed, Is.False);
                Assert.That(lab.State.Biomass, Is.EqualTo(100f));
                Assert.That(lab.Specimens(), Has.Count.EqualTo(63));
            });
        });
    }

    /// <summary>Only dead adults can be claimed, and a corpse's yield is returned exactly once.</summary>
    [Test]
    public async Task HarvestRejectsLivingAndJuvenileSpecimensAndCannotRepeat()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var live = true;
            var juvenile = true;
            var first = false;
            var repeated = true;
            var yield = 0;
            ProtoId<FishXenobiologyBreedPrototype> breed = default;
            await lab.Pair.Server.WaitPost(() =>
            {
                var lifecycle = lab.Pair.Server.System<FishXenobiologySystem>();
                lab.State.Adult = true;
                live = lifecycle.TryHarvest(lab.Slime, out _, out _);
                lab.State.Adult = false;
                lab.Pair.Server.System<MobStateSystem>().ChangeMobState(lab.Slime, MobState.Dead);
                juvenile = lifecycle.TryHarvest(lab.Slime, out _, out _);
                lab.State.Adult = true;
                lab.State.ExtractYield = 3;
                first = lifecycle.TryHarvest(lab.Slime, out breed, out yield);
                repeated = lifecycle.TryHarvest(lab.Slime, out _, out _);
            });
            await lab.Pair.Server.WaitAssertion(() =>
            {
                Assert.That(live, Is.False);
                Assert.That(juvenile, Is.False);
                Assert.That(first, Is.True);
                Assert.That(breed.Id, Is.EqualTo("FishGrey"));
                Assert.That(yield, Is.EqualTo(3));
                Assert.That(repeated, Is.False);
                Assert.That(lab.State.Consumed, Is.True);
            });
        });
    }

    /// <summary>Real container events suspend development and current NPC work, then restore it on release.</summary>
    [Test]
    public async Task ContainerInsertionSuspendsAndReleaseRestoresTheSpecimen()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            EntityUid carrier = default;
            BaseContainer contents = default;
            var inserted = false;
            var removed = false;
            await server.WaitPost(() =>
            {
                carrier = server.EntMan.SpawnEntity("FishXenoCarrier", lab.Coordinates);
                server.System<HTNSystem>().SetHTNEnabled((lab.Slime,
                    server.EntMan.GetComponent<HTNComponent>(lab.Slime)), true);
                contents = server.System<SharedContainerSystem>().EnsureContainer<Container>(carrier, "xeno_carrier");
                inserted = server.System<SharedContainerSystem>().Insert(lab.Slime, contents);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(inserted, Is.True);
                Assert.That(lab.State.Suspended, Is.True);
                Assert.That(server.EntMan.GetComponent<HTNComponent>(lab.Slime).Enabled, Is.False);
                Assert.That(server.System<FishXenobiologySystem>().CanMature(lab.Slime), Is.False);
            });
            await server.WaitPost(() => removed = server.System<SharedContainerSystem>()
                .Remove(lab.Slime, contents, destination: lab.Coordinates));
            await server.WaitAssertion(() =>
            {
                Assert.That(removed, Is.True);
                Assert.That(lab.State.Suspended, Is.False);
                Assert.That(server.EntMan.GetComponent<HTNComponent>(lab.Slime).Enabled, Is.True);
            });
        });
    }
}

internal sealed class FishXenobiologyTestLab(TestPair pair, EntityUid user, EntityUid slime,
    EntityUid map, EntityCoordinates coordinates)
{
    public TestPair Pair { get; } = pair;
    public EntityUid User { get; } = user;
    public EntityUid Slime { get; } = slime;
    public EntityUid Map { get; } = map;
    public EntityCoordinates Coordinates { get; } = coordinates;
    public FishXenobiologySlimeComponent State => Pair.Server.EntMan.GetComponent<FishXenobiologySlimeComponent>(Slime);

    public void SetTemperature(float temperature)
    {
        var component = Pair.Server.EntMan.GetComponent<TemperatureComponent>(Slime);
        Pair.Server.System<SharedTemperatureSystem>().ChangeHeat((Slime, component),
            (temperature - component.Temperature) * component.HeatCapacity, ignoreHeatResistance: true);
    }

    public List<(EntityUid Uid, FishXenobiologySlimeComponent State)> Specimens()
    {
        var result = new List<(EntityUid, FishXenobiologySlimeComponent)>();
        var query = Pair.Server.EntMan.EntityQueryEnumerator<FishXenobiologySlimeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var slime, out var transform))
        {
            if (transform.MapUid == Map)
                result.Add((uid, slime));
        }
        return result;
    }

    public int CountPrototype(string prototype)
    {
        var count = 0;
        var query = Pair.Server.EntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out _, out var metadata, out var transform))
        {
            if (transform.MapUid == Map && metadata.EntityPrototype?.ID == prototype)
                count++;
        }
        return count;
    }

    public static async Task WithLab(Func<FishXenobiologyTestLab, Task> scenario)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid user = default;
        EntityUid slime = default;
        var complete = false;
        try
        {
            await server.WaitPost(() =>
            {
                var mapSystem = server.System<SharedMapSystem>();
                for (var x = 0; x < 5; x++)
                for (var y = 0; y < 4; y++)
                    mapSystem.SetTile(map.Grid, new EntityCoordinates(map.GridCoords.EntityId, x, y), map.Tile.Tile);
                user = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(map.GridCoords.EntityId, 1.5f, 1.5f));
                server.System<SharedGodmodeSystem>().EnableGodmode(user);
                slime = server.EntMan.SpawnEntity("FishXenoSlimeGrey", new EntityCoordinates(map.GridCoords.EntityId, 2.25f, 1.5f));
                server.System<HTNSystem>().SetHTNEnabled((slime, server.EntMan.GetComponent<HTNComponent>(slime)), false);
            });
            await server.WaitRunTicks(1);
            await scenario(new FishXenobiologyTestLab(pair, user, slime, map.MapUid,
                new EntityCoordinates(map.GridCoords.EntityId, 2.25f, 1.5f)));
            complete = true;
        }
        finally
        {
            await server.WaitPost(() => server.EntMan.DeleteEntity(map.MapUid));
            if (complete)
                await pair.CleanReturnAsync();
        }
    }
}
