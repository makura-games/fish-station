#nullable enable
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
using Content.Shared.Inventory;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Fish.Cargo;

/// <summary>Exercises request routing, card-based approval and delivery through a real buying pallet.</summary>
[TestFixture, NonParallelizable]
public sealed class FishDepartmentOrdersWorkflowTest
{
    internal const int InitialBalance = 10000;

    /// <summary>Engineering requests retain their funding account when the requester equips an authorized ID in Cargo.</summary>
    [TestCase("ComputerCargoOrders", "QuartermasterIDCard")]
    [TestCase("ComputerCargoOrders", "CaptainIDCard")]
    [TestCase("BoxFolderQmClipboard", "QuartermasterIDCard")]
    [TestCase("BoxFolderQmClipboard", "CaptainIDCard")]
    public async Task EngineeringRequestRequiresCommandCardAndDebitsEngineering(string approvalPrototype, string cardPrototype)
    {
        await WithStation(async context =>
        {
            var server = context.Pair.Server;
            var approvalConsole = approvalPrototype == "ComputerCargoOrders" ? context.Main : context.Clipboard;
            await EquipCard(context, "EngineeringIDCard");
            await Send(context, context.Department,
                new CargoConsoleAddOrderMessage("Engineer", "RCD refills", "EngineeringRCDAmmo", 1));
            CargoOrderData order = null!;
            await server.WaitAssertion(() =>
            {
                var database = server.EntMan.GetComponent<StationCargoOrderDatabaseComponent>(context.Station);
                Assert.That(database.Orders["Cargo"], Has.Count.EqualTo(1));
                Assert.That(database.Orders["Engineering"], Is.Empty);
                order = database.Orders["Cargo"].Single();
                Assert.That(order.Product.Id, Is.EqualTo("EngineeringRCDAmmo"));
                Assert.That(order.Account.Id, Is.EqualTo("Engineering"));
                var access = server.System<AccessReaderSystem>();
                Assert.That(access.IsAllowed(context.User, context.Department), Is.True);
                Assert.That(access.IsAllowed(context.User, context.Main), Is.False);
                Assert.That(access.IsAllowed(context.User, context.Clipboard), Is.False);
                AssertPending(context, order);
            });

            foreach (var console in new[] { context.Department, context.Main, context.Clipboard })
            {
                await Send(context, console, new CargoConsoleApproveOrderMessage(order.OrderId));
                await server.WaitAssertion(() => AssertPending(context, order));
            }

            await EquipCard(context, cardPrototype);
            await server.WaitAssertion(() => Assert.That(server.System<AccessReaderSystem>()
                .IsAllowed(context.User, approvalConsole), Is.True));
            await Send(context, approvalConsole, new CargoConsoleApproveOrderMessage(order.OrderId));
            await server.WaitAssertion(() =>
            {
                var bank = server.EntMan.GetComponent<StationBankAccountComponent>(context.Station);
                var cost = server.ProtoMan.Index(order.Product).Cost;
                Assert.That(order.Approved, Is.True);
                Assert.That(order.ApprovingConsole, Is.EqualTo(server.EntMan.GetNetEntity(approvalConsole)));
                Assert.That(order.NumDispatched, Is.EqualTo(1));
                Assert.That(server.EntMan.GetComponent<StationCargoOrderDatabaseComponent>(context.Station).AllOrders, Is.Empty);
                foreach (var (account, balance) in bank.Accounts)
                    Assert.That(balance, Is.EqualTo(InitialBalance - (account == order.Account ? cost : 0)), account.Id);
                var delivered = DeliveredProducts(context, order);
                Assert.That(delivered, Has.Count.EqualTo(1));
                var output = server.EntMan.GetComponent<TransformComponent>(context.Pallet);
                var crate = server.EntMan.GetComponent<TransformComponent>(delivered.Single());
                Assert.That(crate.GridUid, Is.EqualTo(output.GridUid));
                Assert.That(crate.LocalPosition, Is.EqualTo(output.LocalPosition));
            });
        });
    }

    /// <summary>Cargo and salvage staff can submit requests but cannot approve their own purchases on either terminal.</summary>
    [TestCase("ComputerCargoOrders", "CargoIDCard")]
    [TestCase("ComputerCargoOrders", "SalvageIDCard")]
    [TestCase("BoxFolderQmClipboard", "CargoIDCard")]
    [TestCase("BoxFolderQmClipboard", "SalvageIDCard")]
    public async Task CargoStaffCannotApproveTheirOwnRequests(string consolePrototype, string cardPrototype)
    {
        await WithStation(async context =>
        {
            var server = context.Pair.Server;
            var console = consolePrototype == "ComputerCargoOrders" ? context.Main : context.Clipboard;
            await EquipCard(context, cardPrototype);
            await Send(context, console,
                new CargoConsoleAddOrderMessage("Cargo staff", "Station materials", "MaterialSteel", 1));
            CargoOrderData order = null!;
            await server.WaitAssertion(() =>
            {
                var database = server.EntMan.GetComponent<StationCargoOrderDatabaseComponent>(context.Station);
                Assert.That(database.Orders["Cargo"], Has.Count.EqualTo(1));
                order = database.Orders["Cargo"].Single();
                Assert.That(order.Account.Id, Is.EqualTo("Cargo"));
                Assert.That(server.System<AccessReaderSystem>().IsAllowed(context.User, console), Is.False);
                AssertPending(context, order);
            });
            await Send(context, console, new CargoConsoleApproveOrderMessage(order.OrderId));
            await server.WaitAssertion(() => AssertPending(context, order));
        });
    }

    internal static async Task Send(StationContext context, EntityUid console, BoundUserInterfaceMessage message)
    {
        var server = context.Pair.Server;
        var opened = false;
        await server.WaitPost(() => opened = server.System<UserInterfaceSystem>()
            .TryOpenUi(console, CargoConsoleUiKey.Orders, context.User));
        await server.WaitAssertion(() => Assert.That(opened, Is.True, "The actor must subscribe before sending a cargo BUI message."));
        await server.WaitPost(() =>
        {
            message.Actor = context.User;
            server.System<UserInterfaceSystem>().RaiseUiMessage(console, CargoConsoleUiKey.Orders, message);
        });
    }

    internal static async Task EquipCard(StationContext context, string? prototype)
    {
        var equipped = false;
        var server = context.Pair.Server;
        await server.WaitPost(() =>
        {
            var inventory = server.System<InventorySystem>();
            if (inventory.TryGetSlotEntity(context.User, "id", out _) && !inventory.TryUnequip(context.User, "id", force: true))
                return;
            if (prototype == null)
            {
                equipped = true;
                return;
            }
            var card = server.EntMan.SpawnEntity(prototype, server.EntMan.GetComponent<TransformComponent>(context.User).Coordinates);
            equipped = inventory.TryEquip(context.User, card, "id", force: true);
        });
        await server.WaitAssertion(() => Assert.That(equipped, Is.True, prototype ?? "remove ID"));
    }

    internal static void AssertPending(StationContext context, CargoOrderData order)
    {
        var entities = context.Pair.Server.EntMan;
        Assert.That(entities.GetComponent<StationCargoOrderDatabaseComponent>(context.Station).Orders["Cargo"], Does.Contain(order));
        Assert.That(order.Approved, Is.False);
        Assert.That(order.ApprovingConsole, Is.Null);
        Assert.That(order.NumDispatched, Is.Zero);
        Assert.That(entities.GetComponent<StationBankAccountComponent>(context.Station).Accounts.Values,
            Is.All.EqualTo(InitialBalance));
        Assert.That(DeliveredProducts(context, order), Is.Empty);
    }

    private static List<EntityUid> DeliveredProducts(StationContext context, CargoOrderData order)
    {
        var entities = context.Pair.Server.EntMan;
        var product = context.Pair.Server.ProtoMan.Index<CargoProductPrototype>(order.Product).Product;
        var map = entities.GetComponent<TransformComponent>(context.Pallet).MapUid;
        var result = new List<EntityUid>();
        var query = entities.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var metadata, out var transform))
        {
            if (metadata.EntityPrototype?.ID == product.Id && transform.MapUid == map)
                result.Add(uid);
        }
        return result;
    }

    internal static async Task WithStation(Func<StationContext, Task> scenario, bool connected = false)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = connected });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var previous = pair.Player?.AttachedEntity;
        EntityUid station = default, department = default, main = default, clipboard = default, user = default, pallet = default;
        var completed = false;
        try
        {
            await server.WaitPost(() =>
            {
                var maps = server.System<SharedMapSystem>();
                for (var x = 0; x < 8; x++)
                for (var y = 0; y < 3; y++)
                    maps.SetTile(map.Grid, new EntityCoordinates(map.Grid, x, y), map.Tile.Tile);
                station = server.EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                server.EntMan.AddComponent<StationDataComponent>(station);
                server.System<StationSystem>().AddGridToStation(station, map.Grid);
                var bank = server.EntMan.AddComponent<StationBankAccountComponent>(station);
                var database = server.EntMan.AddComponent<StationCargoOrderDatabaseComponent>(station);
                foreach (var account in bank.Accounts.Keys)
                    database.Orders.Add(account, new List<CargoOrderData>());
                department = server.EntMan.SpawnEntity("ComputerCargoOrdersEngineering", new EntityCoordinates(map.Grid, 0.5f, 1.5f));
                main = server.EntMan.SpawnEntity("ComputerCargoOrders", new EntityCoordinates(map.Grid, 2.5f, 1.5f));
                clipboard = server.EntMan.SpawnEntity("BoxFolderQmClipboard", new EntityCoordinates(map.Grid, 2.5f, 0.5f));
                user = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 1.5f, 0.5f));
                pallet = server.EntMan.SpawnEntity("CargoPalletBuy", new EntityCoordinates(map.Grid, 6.5f, 0.5f));
                var power = server.System<PowerReceiverSystem>();
                power.SetPowerDisabled(department, false);
                power.SetNeedsPower(department, false);
                power.SetPowerDisabled(main, false);
                power.SetNeedsPower(main, false);
                if (connected)
                    server.PlayerMan.SetAttachedEntity(pair.Player!, user);
            });
            await pair.RunTicksSync(3);
            await server.WaitPost(() =>
            {
                var bank = server.EntMan.GetComponent<StationBankAccountComponent>(station);
                // Fish-edit: нормализуем баланс после первого тика пассивного дохода через публичный API банка.
                foreach (var account in bank.Accounts.Keys.ToArray())
                    server.System<CargoSystem>().UpdateBankAccount((station, bank), InitialBalance - bank.Accounts[account], account);
            });
            await server.WaitAssertion(() => Assert.That(server.EntMan.GetComponent<TransformComponent>(pallet).Anchored, Is.True));
            await scenario(new StationContext(pair, station, department, main, clipboard, user, pallet));
            completed = true;
        }
        finally
        {
            await server.WaitPost(() =>
            {
                foreach (var console in new[] { department, main, clipboard })
                {
                    if (server.EntMan.EntityExists(console))
                        server.System<UserInterfaceSystem>().CloseUi(console, CargoConsoleUiKey.Orders);
                }
                if (connected)
                    server.PlayerMan.SetAttachedEntity(pair.Player!, previous);
                server.EntMan.DeleteEntity(map.MapUid);
                if (station.IsValid())
                    server.EntMan.DeleteEntity(station);
            });
            await pair.RunTicksSync(5);
            if (completed)
                await pair.CleanReturnAsync();
        }
    }

    internal sealed record StationContext(TestPair Pair, EntityUid Station, EntityUid Department, EntityUid Main,
        EntityUid Clipboard, EntityUid User, EntityUid Pallet);
}
