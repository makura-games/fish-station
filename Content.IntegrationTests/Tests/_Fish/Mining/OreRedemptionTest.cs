using Content.Server._Fish.Mining;
using Content.Server.Power.Components;
using Content.Shared._Fish.Mining;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.Mining;

[TestFixture]
public sealed class OreRedemptionTest
{
    private static readonly EntProtoId OreProcessorProto = "OreProcessor";
    private static readonly EntProtoId OreProcessorIndustrialProto = "OreProcessorIndustrial";
    private static readonly EntProtoId SteelOreProto = "SteelOre1";
    private static readonly EntProtoId CoalProto = "Coal1";

    [Test]
    public async Task OreProcessorHasRedemptionComponent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ProtoMan;
        var factory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            Assert.That(protoMan.TryIndex(OreProcessorProto, out EntityPrototype processor));
            Assert.That(processor.TryGetComponent<OreRedemptionMachineComponent>(out _, factory));
            Assert.That(processor.TryGetComponent<LatheComponent>(out _, factory));
            Assert.That(processor.TryGetComponent<MaterialStorageComponent>(out _, factory));

            Assert.That(protoMan.TryIndex(OreProcessorIndustrialProto, out EntityPrototype industrial));
            Assert.That(industrial.TryGetComponent<OreRedemptionMachineComponent>(out _, factory));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AbsorbsNearbyOreAndQueuesSmelting()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var mapData = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var materials = server.System<SharedMaterialStorageSystem>();

        await server.WaitAssertion(() =>
        {
            var processor = entMan.SpawnEntity(OreProcessorProto, mapData.GridCoords);
            if (entMan.TryGetComponent(processor, out ApcPowerReceiverComponent power))
            {
                power.NeedsPower = false;
                power.Powered = true;
            }

            var ore = entMan.SpawnEntity(SteelOreProto, mapData.GridCoords);

            var redemption = server.System<OreRedemptionSystem>();
            var storage = entMan.GetComponent<MaterialStorageComponent>(processor);
            var redemptionComp = entMan.GetComponent<OreRedemptionMachineComponent>(processor);

            materials.UpdateMaterialWhitelist(processor);

            Assert.That(redemption.TryAbsorbOre(processor, ore, storage, redemptionComp), Is.True,
                "Direct ore absorb should succeed");
            Assert.That(entMan.Deleted(ore) || entMan.IsQueuedForDeletion(ore), Is.True);
            Assert.That(materials.GetMaterialAmount(processor, "RawIron"), Is.GreaterThan(0));

            var coal = entMan.SpawnEntity(CoalProto, mapData.GridCoords);
            Assert.That(redemption.TryAbsorbOre(processor, coal, storage, redemptionComp), Is.True);
            redemption.TryAutoProcess(processor);

            var lathe = entMan.GetComponent<LatheComponent>(processor);
            Assert.That(lathe.Queue.Count > 0 || lathe.CurrentRecipe != null,
                "With iron and coal the processor should auto-queue steel smelting");
        });

        await pair.CleanReturnAsync();
    }
}
