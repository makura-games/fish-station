using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Power.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Events;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.Cargo;

/// <summary>Exercises department catalogs and the existing authoritative cargo request path.</summary>
[TestFixture, NonParallelizable]
public sealed class FishDepartmentOrdersTest
{
    /// <summary>Every market product is offered by exactly its department, including shared emergency supplies.</summary>
    [TestCase("ComputerCargoOrders", "MaterialSteel", "ArmorySmg")]
    [TestCase("BoxFolderQmClipboard", "EmergencyBruteKit", "EngineeringRCDAmmo")]
    [TestCase("ComputerCargoOrdersSecurity", "ArmorySmg", "MaterialSteel")]
    [TestCase("ComputerCargoOrdersMedical", "MedicalChemistrySupplies", "EmergencyBruteKit")]
    [TestCase("ComputerCargoOrdersEngineering", "EngineeringRCDAmmo", "MaterialSteel")]
    [TestCase("ComputerCargoOrdersScience", "ArtifactContainer", "ArmorySmg")]
    [TestCase("ComputerCargoOrdersService", "FunSprayPaints", "MedicalChemistrySupplies")]
    public async Task ConsoleCatalogContainsOnlyItsDepartment(string prototype, string allowed, string excluded)
    {
        await WithStation(async (pair, station, console) =>
        {
            await pair.Server.WaitAssertion(() =>
            {
                var available = pair.Server.System<CargoSystem>().GetAvailableProducts(
                    (console, pair.Server.EntMan.GetComponent<CargoOrderConsoleComponent>(console)));
                Assert.That(available.Select(id => id.ToString()), Does.Contain(allowed));
                Assert.That(available.Select(id => id.ToString()), Does.Not.Contain(excluded));
            });
        }, prototype);
    }

    /// <summary>Department request consoles cannot fulfill their own pending requests.</summary>
    [TestCase("ComputerCargoOrdersSecurity")]
    [TestCase("ComputerCargoOrdersMedical")]
    [TestCase("ComputerCargoOrdersEngineering")]
    [TestCase("ComputerCargoOrdersScience")]
    [TestCase("ComputerCargoOrdersService")]
    public async Task DepartmentOrdersAreSentToThePrimaryAccount(string prototype)
    {
        await WithStation(async (pair, station, console) =>
            await pair.Server.WaitAssertion(() => Assert.That(
                pair.Server.EntMan.GetComponent<CargoOrderConsoleComponent>(console).Mode,
                Is.EqualTo(CargoOrderConsoleMode.SendToPrimary))), prototype);
    }

    /// <summary>Main console and clipboard approval readers require a Quartermaster or Captain ID.</summary>
    [TestCase("ComputerCargoOrders")]
    [TestCase("BoxFolderQmClipboard")]
    public async Task ApprovalAccessRequiresTheQuartermasterCard(string prototype)
    {
        await WithStation(async (pair, station, console) =>
        {
            await pair.Server.WaitAssertion(() =>
            {
                var entities = pair.Server.EntMan;
                var reader = pair.Server.System<AccessReaderSystem>();
                foreach (var (cardPrototype, allowed) in new[]
                         {
                             ("CargoIDCard", false), ("SalvageIDCard", false),
                             ("QuartermasterIDCard", true), ("CaptainIDCard", true),
                         })
                {
                    var card = entities.SpawnEntity(cardPrototype, entities.GetComponent<TransformComponent>(console).Coordinates);
                    Assert.That(reader.IsAllowed(card, console), Is.EqualTo(allowed), cardPrototype);
                    entities.DeleteEntity(card);
                }
            });
        }, prototype);
    }

    /// <summary>A forged product ID cannot bypass the department catalog checked by the server.</summary>
    [Test]
    public async Task ForeignProductRequestDoesNotCreateAnOrder()
    {
        await WithStation(async (pair, station, console) =>
        {
            var server = pair.Server;
            EntityUid actor = default;
            var opened = false;
            await server.WaitPost(() =>
            {
                actor = server.EntMan.SpawnEntity("MobHuman", server.EntMan.GetComponent<TransformComponent>(console).Coordinates);
                server.System<PowerReceiverSystem>().SetNeedsPower(console, false);
                opened = server.System<UserInterfaceSystem>().TryOpenUi(console, CargoConsoleUiKey.Orders, actor);
            });
            await server.WaitAssertion(() => Assert.That(opened, Is.True));
            await server.WaitPost(() => server.System<UserInterfaceSystem>().RaiseUiMessage(console, CargoConsoleUiKey.Orders,
                new CargoConsoleAddOrderMessage("test", "valid control", "MaterialSteel", 1) { Actor = actor }));
            await server.WaitAssertion(() => Assert.That(
                server.EntMan.GetComponent<StationCargoOrderDatabaseComponent>(station).AllOrders, Has.Exactly(1).Items));
            await server.WaitPost(() => server.System<UserInterfaceSystem>().RaiseUiMessage(console, CargoConsoleUiKey.Orders,
                new CargoConsoleAddOrderMessage("test", "foreign product", "ArmorySmg", 1) { Actor = actor }));
            await server.WaitAssertion(() => Assert.That(
                server.EntMan.GetComponent<StationCargoOrderDatabaseComponent>(station).AllOrders.Select(order => order.Product.Id),
                Is.EqualTo(new[] { "MaterialSteel" })));
        }, "ComputerCargoOrders");
    }

    private static async Task WithStation(Func<TestPair, EntityUid, EntityUid, Task> scenario, string consolePrototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid station = default;
        EntityUid console = default;
        var completed = false;
        try
        {
            await server.WaitPost(() =>
            {
                station = server.EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                server.EntMan.AddComponent<StationDataComponent>(station);
                server.System<StationSystem>().AddGridToStation(station, map.Grid);
                var bank = server.EntMan.AddComponent<StationBankAccountComponent>(station);
                var database = server.EntMan.AddComponent<StationCargoOrderDatabaseComponent>(station);
                foreach (var account in bank.Accounts.Keys)
                    database.Orders.TryAdd(account, new List<CargoOrderData>());
                console = server.EntMan.SpawnEntity(consolePrototype, map.GridCoords);
            });
            await scenario(pair, station, console);
            completed = true;
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.EntMan.DeleteEntity(map.MapUid);
                if (station.IsValid())
                    server.EntMan.DeleteEntity(station);
            });
            if (completed)
                await pair.CleanReturnAsync();
            else
                await pair.DisposeAsync();
        }
    }
}
