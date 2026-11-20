#nullable enable
using Content.Server.NPC.HTN;
using Content.Shared._Fish.NPC;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.NPC;

[TestFixture]
public sealed class FishNpcBrainTests
{
    private static readonly EntProtoId AssaultPirate = "MobPirateT1Knife";
    private static readonly EntProtoId DefensePirate = "MobPirateT2Medic";

    [Test]
    public async Task HostileMobsHaveFishBrainDoctrines()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var assault = EntityUid.Invalid;
        var defense = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            assault = entMan.SpawnEntity(AssaultPirate, testMap.GridCoords);
            defense = entMan.SpawnEntity(DefensePirate, testMap.GridCoords);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent(assault, out FishNpcBrainComponent? assaultBrain), Is.True);
            Assert.That(assaultBrain!.Doctrine, Is.EqualTo(FishNpcDoctrine.Assault));

            Assert.That(entMan.TryGetComponent(defense, out FishNpcBrainComponent? defenseBrain), Is.True);
            Assert.That(defenseBrain!.Doctrine, Is.EqualTo(FishNpcDoctrine.Defense));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BrainDisablesHtnOnMapInit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var uid = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            uid = entMan.SpawnEntity(AssaultPirate, testMap.GridCoords);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<FishNpcBrainComponent>(uid), Is.True);
            Assert.That(entMan.TryGetComponent(uid, out HTNComponent? htn), Is.True);
            Assert.That(htn!.Enabled, Is.False, "HTN должен быть выключен мозгом");
        });

        await pair.CleanReturnAsync();
    }
}
