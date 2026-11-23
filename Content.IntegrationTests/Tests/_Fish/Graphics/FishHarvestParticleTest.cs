using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Fish.Graphics;

public sealed class FishHarvestParticleTest : InteractionTest
{
    [TestCase("PoppySeeds", "FoodPoppy", 3, "FishPollenParticle", 6)]
    [TestCase("SunflowerSeeds", "FoodSunflower", 1, "FishPollenParticle", 6)]
    [TestCase("ChanterelleSeeds", "FoodMushroom", 5, "FishSporesParticle", 4)]
    [TestCase("WheatSeeds", "WheatBushel", 3, "FishLeavesParticle", 5)]
    public async Task TrayEmitsOneBurstOnRipeHarvest(string seeds, string produce, int expectedYield, string particle, int count)
    {
        var tray = await SpawnTarget("hydroponicsTray");
        await InteractUsing(seeds);
        await Interact(awaitDoAfters: false);

        Task AssertParticles(int expected) => Client.WaitAssertion(() =>
        {
            var particles = CEntMan.EntityQuery<MetaDataComponent>()
                .Select(meta => meta.EntityPrototype?.ID)
                .Where(id => id is "FishPollenParticle" or "FishSporesParticle" or "FishLeavesParticle")
                .ToArray();
            Assert.That(particles, Has.Length.EqualTo(expected));
            Assert.That(particles.All(id => id == particle), Is.True);
        });

        await AssertParticles(0);
        await Server.WaitAssertion(() =>
        {
            var trays = Server.System<PlantTraySystem>();
            Assert.That(trays.TryGetAlivePlant(ToServer(tray), out var plant), Is.True);
            Server.System<PlantHolderSystem>().AdjustsAge(plant!.Value, 15);
            Server.System<PlantSystem>().UpdatePlant(plant.Value, force: true);
            Assert.That(SEntMan.GetComponent<PlantHolderComponent>(plant.Value).ReadyForHarvest, Is.True);
        });
        await RunTicks((int) (1 / TickPeriod) + 1);

        await Interact(awaitDoAfters: false);
        await Server.WaitAssertion(() => Assert.That(SEntMan.EntityQuery<MetaDataComponent>()
            .Count(meta => meta.EntityPrototype?.ID == produce), Is.EqualTo(expectedYield)));
        await AssertParticles(count);
        await RunTicks((int) (1 / TickPeriod) + 1);
        await AssertParticles(0);
    }
}
