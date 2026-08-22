using Content.Shared._Fish.BattleShuttles.Components;
using Content.Shared.Mech.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Fish;

[TestFixture]
public sealed class BattleShuttleTest
{
    [Test]
    public async Task SpawnBattleShuttleCivilian_HasMechComponents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid shuttle = default;
        await server.WaitPost(() => shuttle = server.EntMan.SpawnEntity("BattleShuttleCivilian", map.GridCoords));

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<BattleShuttleComponent>(shuttle));
            Assert.That(server.EntMan.HasComponent<MechComponent>(shuttle));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpawnBattleShuttleSecurity_HasStartingEquipment()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid shuttle = default;
        await server.WaitPost(() => shuttle = server.EntMan.SpawnEntity("BattleShuttleSecurity", map.GridCoords));
        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.TryGetComponent<MechComponent>(shuttle, out var mech));
            Assert.That(mech!.EquipmentContainer.ContainedEntities.Count, Is.GreaterThan(0));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpawnHeavySyndicate_KeepsEquipmentOnClosedHatch()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid shuttle = default;
        await server.WaitPost(() => shuttle = server.EntMan.SpawnEntity("BattleShuttleSyndicate", map.GridCoords));
        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.TryGetComponent<BattleShuttleComponent>(shuttle, out var comp));
            Assert.That(comp!.HatchOpen, Is.False);
            Assert.That(server.EntMan.TryGetComponent<MechComponent>(shuttle, out var mech));
            Assert.That(mech!.EquipmentContainer.ContainedEntities.Count, Is.GreaterThan(0));
        });

        await pair.CleanReturnAsync();
    }
}
