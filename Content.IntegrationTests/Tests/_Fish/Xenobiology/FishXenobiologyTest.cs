using Content.IntegrationTests.Pair;
using System.Linq;
using Content.Client._Fish.Xenobiology;
using Content.Server._Fish.Xenobiology;
using Content.Server.Lathe;
using Content.Server.Power.EntitySystems;
using Content.Shared._Fish.Xenobiology;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Lathe;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Client.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.Xenobiology;

[TestFixture, NonParallelizable]
public sealed class FishXenobiologyTest
{
    [Test]
    public async Task LaboratoryContentIsAvailable()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
        await pair.Server.WaitAssertion(() =>
        {
            foreach (var id in new[] { "FishXenoSlimeGrey", "FishXenoSlimeGreyAdult", "FishXenoProcessor",
                         "FishXenoCarrier", "FishXenoConsole", "FishXenoCultureGrey", "FishXenoExtractGrey" })
            {
                Assert.That(prototypes.HasIndex<EntityPrototype>(id), Is.True, $"Missing laboratory entity {id}.");
            }
            var lathes = pair.GetPrototypesWithComponent<LatheComponent>().ToList();
            var lathe = pair.Server.System<LatheSystem>();
            var carriers = lathe.GetAllPossibleRecipes(lathes.Single(entry => entry.Item1.ID == "Protolathe").Item2)
                .Select(recipe => recipe.ToString()).ToArray();
            var boards = lathe.GetAllPossibleRecipes(lathes.Single(entry => entry.Item1.ID == "CircuitImprinter").Item2)
                .Select(recipe => recipe.ToString()).ToArray();
            Assert.That(carriers, Does.Contain("FishXenoCarrier").And.Contain("FishXenoCarrierAdvanced"));
            Assert.That(boards, Does.Contain("FishXenoProcessorBoard").And.Contain("FishXenoConsoleBoard")
                .And.Contain("FishXenoThermostatBoard"));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CarrierTransfersCorpseAndPowerLossDoesNotSpendItsExtracts()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var devices = server.System<FishXenobiologyDeviceSystem>();
        var containers = server.System<SharedContainerSystem>();
        EntityUid user = default, carrier = default, corpse = default, processor = default;
        var captured = false;
        var transferred = false;
        var started = false;
        var completed = false;
        try
        {
            await server.WaitPost(() =>
            {
                var mapSystem = server.System<SharedMapSystem>();
                for (var x = 0; x < 5; x++)
                for (var y = 0; y < 4; y++)
                    mapSystem.SetTile(map.Grid, new EntityCoordinates(map.Grid, x, y), map.Tile.Tile);
                var coordinates = new EntityCoordinates(map.Grid, 1.5f, 1.5f);
                user = server.EntMan.SpawnEntity("MobHuman", coordinates);
                server.System<SharedGodmodeSystem>().EnableGodmode(user);
                carrier = server.EntMan.SpawnEntity("FishXenoCarrier", coordinates);
                corpse = server.EntMan.SpawnEntity("FishXenoSlimeGreyAdult", new EntityCoordinates(map.Grid, 2.25f, 1.5f));
                processor = server.EntMan.SpawnEntity("FishXenoProcessor", new EntityCoordinates(map.Grid, 1.5f, 2.5f));
                server.System<MobStateSystem>().ChangeMobState(corpse, MobState.Dead);
                server.System<SharedHandsSystem>().TryPickupAnyHand(user, carrier);
                captured = devices.TryCapture((carrier, server.EntMan.GetComponent<FishXenobiologyCarrierComponent>(carrier)), user, corpse);
            });
            await pair.RunSeconds(2.4f);
            await server.WaitAssertion(() =>
            {
                Assert.That(captured, Is.True);
                Assert.That(containers.IsEntityInContainer(corpse), Is.True);
                Assert.That(server.EntMan.GetComponent<FishXenobiologySlimeComponent>(corpse).Suspended, Is.True);
            });
            await server.WaitPost(() =>
            {
                var machine = (processor, server.EntMan.GetComponent<FishXenobiologyProcessorComponent>(processor));
                transferred = devices.TryTransfer((carrier, server.EntMan.GetComponent<FishXenobiologyCarrierComponent>(carrier)), user, machine);
                server.System<PowerReceiverSystem>().SetNeedsPower(processor, false);
            });
            await pair.RunTicksSync(3);
            await server.WaitPost(() => started = devices.TryProcess(
                (processor, server.EntMan.GetComponent<FishXenobiologyProcessorComponent>(processor)), user));
            await pair.RunSeconds(2f);
            await server.WaitAssertion(() =>
            {
                Assert.That(transferred, Is.True);
                Assert.That(started, Is.True);
                Assert.That(server.EntMan.GetComponent<FishXenobiologySlimeComponent>(corpse).Consumed, Is.False);
            });
            await server.WaitPost(() => server.System<PowerReceiverSystem>().SetNeedsPower(processor, true));
            await pair.RunSeconds(5f);
            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.EntityExists(corpse), Is.True);
                Assert.That(server.EntMan.GetComponent<FishXenobiologySlimeComponent>(corpse).Consumed, Is.False);
                Assert.That(server.EntMan.HasComponent<FishXenobiologyProcessingComponent>(processor), Is.False);
            });
            await server.WaitPost(() =>
            {
                server.System<PowerReceiverSystem>().SetNeedsPower(processor, false);
            });
            await pair.RunTicksSync(3);
            await server.WaitPost(() => started = devices.TryProcess(
                (processor, server.EntMan.GetComponent<FishXenobiologyProcessorComponent>(processor)), user));
            await pair.RunSeconds(6.2f);
            await server.WaitAssertion(() =>
            {
                Assert.That(started, Is.True);
                Assert.That(server.EntMan.EntityExists(corpse), Is.False);
                Assert.That(server.EntMan.EntityQueryEnumerator<FishXenobiologyExtractComponent>()
                    .MoveNext(out _, out _), Is.True);
            });
            completed = true;
        }
        finally
        {
            await server.WaitPost(() => server.EntMan.DeleteEntity(map.MapUid));
            if (completed)
                await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task ConsoleOpensNativeClientWindowWithMeasurementsAndCatalog()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var previous = pair.Player.AttachedEntity;
        EntityUid console = default;
        var opened = false;
        var completed = false;
        try
        {
            await server.WaitPost(() =>
            {
                var mapSystem = server.System<SharedMapSystem>();
                for (var x = 0; x < 5; x++)
                for (var y = 0; y < 4; y++)
                    mapSystem.SetTile(map.Grid, new EntityCoordinates(map.Grid, x, y), map.Tile.Tile);
                console = server.EntMan.SpawnEntity("FishXenoConsole", new EntityCoordinates(map.Grid, 1.5f, 2.5f));
                server.System<PowerReceiverSystem>().SetNeedsPower(console, false);
                var user = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 1.5f, 1.5f));
                server.EntMan.SpawnEntity("FishXenoSlimeGrey", new EntityCoordinates(map.Grid, 2.25f, 1.5f));
                server.EntMan.SpawnEntity("FishXenoSlimeGrey", new EntityCoordinates(map.Grid, 3.25f, 1.5f));
                server.PlayerMan.SetAttachedEntity(pair.Player, user);
                opened = server.System<UserInterfaceSystem>().TryOpenUi(console, FishXenobiologyUiKey.Key, user);
            });
            await pair.RunTicksSync(20);
            await pair.RunSeconds(1.2f);
            await server.WaitAssertion(() =>
            {
                Assert.That(opened, Is.True);
                Assert.That(server.System<UserInterfaceSystem>()
                    .TryGetUiState<FishXenobiologyBoundUserInterfaceState>(console, FishXenobiologyUiKey.Key, out var snapshot), Is.True);
                var state = snapshot!;
                Assert.That(state.Specimens.Count, Is.EqualTo(2));
                Assert.That(state.Specimens.Select(specimen => specimen.Entity).Distinct().Count(), Is.EqualTo(2));
                Assert.That(state.Specimens.Select(specimen => specimen.Identifier), Is.All.Not.Empty);
                Assert.That(state.Specimens.Select(specimen => specimen.Identifier).Distinct().Count(), Is.EqualTo(2),
                    "Two individuals of the same breed must have distinct visible identifiers.");
            });
            await pair.Client.WaitAssertion(() =>
            {
                var root = pair.Client.ResolveDependency<IUserInterfaceManager>().WindowRoot;
                var window = root.Children.OfType<FishXenobiologyWindow>().Single();
                Assert.That(window.SpecimenList.Children.Count(child => child.Visible), Is.EqualTo(2));
                Assert.That(window.SelectedBreed, Is.Not.Null.And.Not.Empty);
                Assert.That(window.BreedDetails.ChildCount, Is.GreaterThan(0));
                Assert.That(root.Children.OfType<FishXenobiologyProcessorWindow>(), Is.Empty);
            });
            completed = true;
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.System<UserInterfaceSystem>().CloseUi(console, FishXenobiologyUiKey.Key);
                server.PlayerMan.SetAttachedEntity(pair.Player, previous);
                server.EntMan.DeleteEntity(map.MapUid);
            });
            await pair.RunTicksSync(5);
            if (completed)
                await pair.CleanReturnAsync();
        }
    }
}
