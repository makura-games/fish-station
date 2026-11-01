#nullable enable
using System.Linq;
using Content.Client.Cargo.BUI;
using Content.Client.Cargo.UI;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Events;
using Robust.Client.UserInterface;
using ClientUserInterfaceSystem = Robust.Client.GameObjects.UserInterfaceSystem;
using ServerUserInterfaceSystem = Robust.Server.GameObjects.UserInterfaceSystem;

namespace Content.IntegrationTests.Tests._Fish.Cargo;

/// <summary>Checks approval controls against replicated ID access while a real Cargo BUI remains open.</summary>
[TestFixture, NonParallelizable]
public sealed class FishDepartmentOrdersUiTest
{
    /// <summary>Exchanging the user's ID updates the pending order's approval button without reopening the terminal.</summary>
    [TestCase("ComputerCargoOrders", "CargoIDCard")]
    [TestCase("ComputerCargoOrders", "SalvageIDCard")]
    [TestCase("BoxFolderQmClipboard", "CargoIDCard")]
    [TestCase("BoxFolderQmClipboard", "SalvageIDCard")]
    public async Task OpenConsoleRefreshesApprovalWhenIdChanges(string consolePrototype, string staffCard)
    {
        await FishDepartmentOrdersWorkflowTest.WithStation(async context =>
        {
            var pair = context.Pair;
            var server = pair.Server;
            var client = pair.Client;
            var console = consolePrototype == "ComputerCargoOrders" ? context.Main : context.Clipboard;
            await FishDepartmentOrdersWorkflowTest.EquipCard(context, staffCard);
            var opened = false;
            await server.WaitPost(() => opened = server.System<ServerUserInterfaceSystem>()
                .TryOpenUi(console, CargoConsoleUiKey.Orders, context.User));
            await pair.RunTicksSync(15);
            await server.WaitAssertion(() => Assert.That(opened, Is.True, "Cargo browsing must be available to ordinary staff."));
            CargoOrderConsoleBoundUserInterface bui = null!;
            CargoConsoleMenu window = null!;
            await client.WaitAssertion(() =>
            {
                Assert.That(client.System<ClientUserInterfaceSystem>().TryGetOpenUi<CargoOrderConsoleBoundUserInterface>(
                    pair.ToClientUid(console), CargoConsoleUiKey.Orders, out var openedUi), Is.True);
                bui = openedUi!;
                window = client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CargoConsoleMenu>().Single();
                Assert.That(window.IsOpen, Is.True);
            });

            await client.WaitPost(() => bui.SendMessage(new CargoConsoleAddOrderMessage("Cargo staff", "Materials", "MaterialSteel", 1)));
            await pair.RunTicksSync(15);
            await client.WaitAssertion(() =>
            {
                Assert.That(Rows(window), Has.Length.EqualTo(1));
                Assert.That(Rows(window).Single().Approve.Visible, Is.True);
                Assert.That(Rows(window).Single().Approve.Disabled, Is.True);
            });

            foreach (var (card, allowed) in new (string? Card, bool Allowed)[]
                     { ("QuartermasterIDCard", true), (null, false), ("CaptainIDCard", true), (staffCard, false) })
            {
                await FishDepartmentOrdersWorkflowTest.EquipCard(context, card);
                await pair.RunTicksSync(15);
                await client.WaitAssertion(() =>
                {
                    Assert.That(window.IsOpen, Is.True);
                    Assert.That(client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children
                        .OfType<CargoConsoleMenu>().Single(), Is.SameAs(window));
                    Assert.That(Rows(window), Has.Length.EqualTo(1));
                    Assert.That(Rows(window).Single().Approve.Disabled, Is.EqualTo(!allowed), card ?? "no ID");
                });
            }
        }, connected: true);
    }

    private static CargoOrderRow[] Rows(CargoConsoleMenu window) =>
        window.FindControl<Control>("Requests").Children.OfType<CargoOrderRow>().ToArray();
}
