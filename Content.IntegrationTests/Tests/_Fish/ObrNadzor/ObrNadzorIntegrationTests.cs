using Content.Shared._Fish.ObrNadzor;
using Robust.Shared.GameObjects;
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
        var entMan = server.EntMan;
        var console = EntityUid.Invalid;

        await server.WaitAssertion(() =>
        {
            Assert.That(proto.HasIndex(ConsoleId));
            Assert.That(proto.HasIndex(EyeId));
            Assert.That(proto.HasIndex(new EntProtoId("FishActionObrNadzorRelease")));
        });

        await server.WaitPost(() =>
        {
            console = entMan.Spawn(ConsoleId);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<ObrNadzorConsoleComponent>(console));

            var comp = entMan.GetComponent<ObrNadzorConsoleComponent>(console);
            Assert.That(comp.BlackoutDuration, Is.EqualTo(TimeSpan.FromSeconds(20)));
            Assert.That(comp.RemoteEntityProto, Is.EqualTo(EyeId));
        });

        await server.WaitPost(() => entMan.DeleteEntity(console));
        await pair.CleanReturnAsync();
    }
}
