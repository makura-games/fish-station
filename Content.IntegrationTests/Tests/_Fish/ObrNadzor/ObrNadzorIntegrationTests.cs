using Content.Shared._Fish.ObrNadzor;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.ObrNadzor;

[TestFixture]
[TestOf(typeof(ObrNadzorConsoleComponent))]
public sealed class ObrNadzorIntegrationTests
{
    private static readonly EntProtoId ConsoleId = "FishComputerObrNadzor";
    private static readonly EntProtoId EyeId = "FishObrNadzorConsoleEye";

    [Test]
    public async Task ConsolePrototypeSpawnsWithComponent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            Assert.That(proto.HasIndex(ConsoleId));
            Assert.That(proto.HasIndex(EyeId));

            var console = entMan.Spawn(ConsoleId);
            Assert.That(entMan.HasComponent<ObrNadzorConsoleComponent>(console));

            var comp = entMan.GetComponent<ObrNadzorConsoleComponent>(console);
            Assert.That(comp.BlackoutDuration, Is.EqualTo(TimeSpan.FromSeconds(20)));
            Assert.That(comp.RemoteEntityProto, Is.EqualTo(EyeId));

            entMan.DeleteEntity(console);
        });

        await pair.CleanReturnAsync();
    }
}
