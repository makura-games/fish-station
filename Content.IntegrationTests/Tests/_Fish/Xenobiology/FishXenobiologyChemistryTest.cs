using Content.Server._Fish.Xenobiology;
using Content.Shared._Fish.Xenobiology;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Fish.Xenobiology;

[TestFixture, NonParallelizable]
public sealed class FishXenobiologyChemistryTest
{
    /// <summary>Nested chemistry callbacks and excess catalyst cannot duplicate finite material outputs.</summary>
    [Test]
    public async Task ExtractReactsOnceWithFixedYieldThroughNativeSolutionEvents()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            EntityUid extract = default;
            var added = false;
            var repeated = true;
            await server.WaitPost(() =>
            {
                extract = server.EntMan.SpawnEntity("FishXenoExtractMetal", lab.Coordinates);
                var solutions = server.System<SharedSolutionContainerSystem>();
                if (solutions.TryGetSolution(extract, "extract", out var solution, out _))
                {
                    added = solutions.TryAddReagent(solution.Value, "Water", FixedPoint2.New(15));
                    repeated = server.System<FishXenobiologyExtractSystem>().TryReact((extract,
                        server.EntMan.GetComponent<FishXenobiologyExtractComponent>(extract)));
                    solutions.TryAddReagent(solution.Value, "Water", FixedPoint2.New(1));
                }
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(added, Is.True);
                Assert.That(repeated, Is.False);
                Assert.That(server.EntMan.GetComponent<FishXenobiologyExtractComponent>(extract).Used, Is.True);
                Assert.That(lab.CountPrototype("SheetSteel1"), Is.EqualTo(1));
                Assert.That(lab.CountPrototype("SheetGlass1"), Is.EqualTo(1));
            });
        });
    }

    /// <summary>Hydration inside a hand container remains dormant until the native drop event.</summary>
    [Test]
    public async Task HydratedCultureHatchesOnlyAfterBeingDropped()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            EntityUid culture = default;
            var pickedUp = false;
            var dropped = false;
            await server.WaitPost(() =>
            {
                culture = server.EntMan.SpawnEntity("FishXenoCultureGrey", lab.Coordinates);
                pickedUp = server.System<SharedHandsSystem>().TryPickupAnyHand(lab.User, culture);
                var solutions = server.System<SharedSolutionContainerSystem>();
                if (solutions.TryGetSolution(culture, "culture", out var solution, out _))
                    solutions.TryAddReagent(solution.Value, "Water", FixedPoint2.New(5));
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(pickedUp, Is.True);
                Assert.That(lab.Specimens(), Has.Count.EqualTo(1));
                Assert.That(server.EntMan.GetComponent<FishXenobiologyCultureComponent>(culture).Used, Is.False);
            });
            await server.WaitPost(() => dropped = server.System<SharedHandsSystem>().TryDrop((lab.User, null), culture));
            await server.WaitRunTicks(1);
            await server.WaitAssertion(() =>
            {
                Assert.That(dropped, Is.True);
                Assert.That(lab.Specimens(), Has.Count.EqualTo(2));
                Assert.That(server.EntMan.EntityExists(culture), Is.False);
            });
        });
    }

    /// <summary>Hydrated cultures cannot create creatures on empty space outside the laboratory floor.</summary>
    [Test]
    public async Task CultureRequiresARealFloorTile()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            EntityUid culture = default;
            var hatched = true;
            await server.WaitPost(() =>
            {
                culture = server.EntMan.SpawnEntity("FishXenoCultureGrey",
                    new EntityCoordinates(lab.Coordinates.EntityId, 20, 20));
                var solutions = server.System<SharedSolutionContainerSystem>();
                if (solutions.TryGetSolution(culture, "culture", out var solution, out _))
                    solutions.TryAddReagent(solution.Value, "Water", FixedPoint2.New(5));
                hatched = server.System<FishXenobiologyExtractSystem>().TryHatch((culture,
                    server.EntMan.GetComponent<FishXenobiologyCultureComponent>(culture)));
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(hatched, Is.False);
                Assert.That(lab.Specimens(), Has.Count.EqualTo(1));
                Assert.That(server.EntMan.GetComponent<FishXenobiologyCultureComponent>(culture).Used, Is.False);
            });
        });
    }

    /// <summary>A capped trait leaves the serum intact; a successful treatment spends it exactly once.</summary>
    [Test]
    public async Task CappedSerumIsNotConsumed()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            EntityUid serum = default;
            var equipped = false;
            var capped = true;
            var applied = false;
            var repeated = true;
            await server.WaitPost(() =>
            {
                serum = server.EntMan.SpawnEntity("FishXenoSerumDocility", lab.Coordinates);
                equipped = server.System<SharedHandsSystem>().TryPickupAnyHand(lab.User, serum);
                lab.State.Docility = 1;
                capped = server.System<FishXenobiologyExtractSystem>().TryApplySerum((serum,
                    server.EntMan.GetComponent<FishXenobiologySerumComponent>(serum)), lab.User, lab.Slime);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(equipped, Is.True);
                Assert.That(capped, Is.False);
                Assert.That(server.EntMan.GetComponent<FishXenobiologySerumComponent>(serum).Used, Is.False);
                Assert.That(server.System<SharedHandsSystem>().IsHolding(lab.User, serum), Is.True);
            });
            await server.WaitPost(() =>
            {
                lab.State.Docility = 0.5f;
                var item = server.EntMan.GetComponent<FishXenobiologySerumComponent>(serum);
                applied = server.System<FishXenobiologyExtractSystem>().TryApplySerum((serum, item), lab.User, lab.Slime);
                repeated = server.System<FishXenobiologyExtractSystem>().TryApplySerum((serum, item), lab.User, lab.Slime);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(applied, Is.True);
                Assert.That(repeated, Is.False);
                Assert.That(lab.State.Docility, Is.EqualTo(0.75f));
            });
        });
    }
}
