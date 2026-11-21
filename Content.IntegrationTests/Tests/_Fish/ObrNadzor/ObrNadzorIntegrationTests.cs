using Content.Shared._Fish.ObrNadzor;
using Content.Shared.Movement.Components;
using Content.Shared.Silicons.StationAi;
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

    [Test]
    public async Task DestroyingConsoleClearsEyeSession()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var console = EntityUid.Invalid;
        var actor = EntityUid.Invalid;
        var eye = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            console = entMan.Spawn(ConsoleId);
            actor = entMan.Spawn(EyeId);
            eye = entMan.Spawn(EyeId);

            var comp = entMan.GetComponent<ObrNadzorConsoleComponent>(console);
            comp.OperatorBody = actor;
            comp.RemoteEntity = entMan.GetNetEntity(eye);

            entMan.EnsureComponent<RelayInputMoverComponent>(actor);
            entMan.EnsureComponent<StationAiOverlayComponent>(actor);

            var session = entMan.EnsureComponent<ObrNadzorSessionComponent>(actor);
            session.Console = console;

            var marker = entMan.EnsureComponent<ObrNadzorEyeComponent>(eye);
            marker.Console = console;

            entMan.DeleteEntity(console);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(console), Is.True);
            Assert.That(entMan.Deleted(eye), Is.True);
            Assert.That(entMan.Deleted(actor), Is.False);
            Assert.That(entMan.HasComponent<RelayInputMoverComponent>(actor), Is.False);
            Assert.That(entMan.HasComponent<StationAiOverlayComponent>(actor), Is.False);
            Assert.That(entMan.HasComponent<ObrNadzorSessionComponent>(actor), Is.False);
        });

        await server.WaitPost(() => entMan.DeleteEntity(actor));
        await pair.CleanReturnAsync();
    }
}
