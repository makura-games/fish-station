using System.Linq;
using Content.Shared._Fish.Shipyard;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.Shipyard;

[TestFixture]
[TestOf(typeof(VesselPrototype))]
public sealed class ShipyardPrototypeTests
{
    private static readonly EntProtoId ComputerShipyardProto = "ComputerShipyard";

    [Test]
    public async Task VesselPrototypesHaveValidShuttlePaths()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();

        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var resMan = server.ResolveDependency<Robust.Shared.ContentPack.IResourceManager>();

        await server.WaitAssertion(() =>
        {
            var vessels = prototypes.EnumeratePrototypes<VesselPrototype>().ToList();
            Assert.That(vessels, Is.Not.Empty, "Expected at least one vessel prototype");

            foreach (var vessel in vessels)
            {
                Assert.That(vessel.Price, Is.GreaterThan(0), vessel.ID);
                Assert.That(vessel.ShuttlePath.ToString(), Is.Not.Empty, vessel.ID);
                Assert.That(resMan.ContentFileExists(vessel.ShuttlePath), Is.True,
                    $"Missing shuttle map for {vessel.ID}: {vessel.ShuttlePath}");
            }

            Assert.That(prototypes.TryIndex(ComputerShipyardProto, out var console), Is.True);
            Assert.That(console!.Components.ContainsKey("ShipyardConsole"), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
