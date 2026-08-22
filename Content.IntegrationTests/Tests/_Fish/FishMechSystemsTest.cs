using Content.Shared._Fish.Mechs.Components;
using Content.Shared.Mech.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Fish;

[TestFixture]
public sealed class FishMechSystemsTest
{
    [Test]
    public async Task SpawnGygax_HasFishMechCoreAndOverload()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid mech = default;
        await server.WaitPost(() => mech = server.EntMan.SpawnEntity("MechGygax", map.GridCoords));

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<MechComponent>(mech));
            Assert.That(server.EntMan.HasComponent<MechInternalDamageComponent>(mech));
            Assert.That(server.EntMan.HasComponent<MechFacingArmorComponent>(mech));
            Assert.That(server.EntMan.HasComponent<MechDualEquipmentComponent>(mech));
            Assert.That(server.EntMan.HasComponent<MechOverloadComponent>(mech));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpawnDurand_HasDefenceMode()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid mech = default;
        await server.WaitPost(() => mech = server.EntMan.SpawnEntity("MechDurand", map.GridCoords));

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<MechDefenceModeComponent>(mech));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpawnMarauder_HasThrustersSmokeStrafe()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid mech = default;
        await server.WaitPost(() => mech = server.EntMan.SpawnEntity("MechMarauder", map.GridCoords));

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<MechThrustersComponent>(mech));
            Assert.That(server.EntMan.HasComponent<MechSmokeComponent>(mech));
            Assert.That(server.EntMan.HasComponent<MechStrafeComponent>(mech));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpawnOdysseus_HasMedicalMechComponents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid mech = default;
        await server.WaitPost(() => mech = server.EntMan.SpawnEntity("MechOdysseus", map.GridCoords));

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<MechComponent>(mech));
            Assert.That(server.EntMan.HasComponent<MechInternalDamageComponent>(mech));
            Assert.That(server.EntMan.HasComponent<MechCabinAtmosComponent>(mech));
            Assert.That(server.EntMan.HasComponent<MechRadioComponent>(mech));
            Assert.That(server.EntMan.HasComponent<MechTrackingBeaconComponent>(mech));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpawnPhazon_HasPhasingAndDamtype()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid mech = default;
        await server.WaitPost(() => mech = server.EntMan.SpawnEntity("MechPhazon", map.GridCoords));

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<MechPhasingComponent>(mech));
            Assert.That(server.EntMan.HasComponent<MechDamtypeCycleComponent>(mech));
        });

        await pair.CleanReturnAsync();
    }
}
