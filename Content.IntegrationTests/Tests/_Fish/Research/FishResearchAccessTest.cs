#nullable enable
using System.Linq;
using Content.Client._Fish.Research.UI;
using Content.IntegrationTests.Pair;
using Content.Server.Power.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using ServerResearchSystem = Content.Server.Research.Systems.ResearchSystem;

namespace Content.IntegrationTests.Tests._Fish.Research;

/// <summary>Research remains readable while purchases require a director-level ID card.</summary>
[TestFixture, NonParallelizable]
public sealed class FishResearchAccessTest
{
    private const int InitialPoints = 100000;

    /// <summary>The purchase handler checks current ID access independently of the client's disabled button.</summary>
    [TestCase(null, false)]
    [TestCase("ResearchIDCard", false)]
    [TestCase("RDIDCard", true)]
    [TestCase("CaptainIDCard", true)]
    public async Task OnlyDirectorAccessCanPurchaseFromAnOpenResearchConsole(string? cardPrototype, bool allowed)
    {
        await WithConsole(cardPrototype, async context =>
        {
            var pair = context.Pair;
            var server = pair.Server;
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(context.Window.Graph.VisibleTechnologyIds, Does.Contain(context.Technology));
                Assert.That(context.Window.ResearchButton.Disabled, Is.EqualTo(!allowed),
                    $"Selected={context.Window.SelectedTechnology}; status={context.Window.PurchaseStatusLabel.Text}");
            });
            await server.WaitPost(() => SendPurchase(context));
            await server.WaitAssertion(() => AssertPurchase(context, allowed));
        });
    }

    /// <summary>Removing the director's ID revokes both the open window's action and the server purchase permission.</summary>
    [Test]
    public async Task RemovingDirectorIdRevokesAccessWithoutClosingTheResearchTree()
    {
        await WithConsole("RDIDCard", async context =>
        {
            var pair = context.Pair;
            var removed = false;
            await pair.Client.WaitAssertion(() => Assert.That(context.Window.ResearchButton.Disabled, Is.False));
            await pair.Server.WaitPost(() => removed = pair.Server.System<InventorySystem>()
                .TryUnequip(context.User, "id", force: true));
            await pair.RunTicksSync(15);
            await pair.Server.WaitAssertion(() => Assert.That(removed, Is.True));
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(context.Window.Graph.VisibleTechnologyIds, Does.Contain(context.Technology));
                Assert.That(context.Window.ResearchButton.Disabled, Is.True);
            });
            await pair.Server.WaitPost(() => SendPurchase(context));
            await pair.Server.WaitAssertion(() => AssertPurchase(context, false));
        });
    }

    private static void SendPurchase(ConsoleContext context)
    {
        var message = new ConsoleUnlockTechnologyMessage(context.Technology) { Actor = context.User };
        context.Pair.Server.System<UserInterfaceSystem>().RaiseUiMessage(context.Console, ResearchConsoleUiKey.Key, message);
    }

    private static void AssertPurchase(ConsoleContext context, bool allowed)
    {
        var server = context.Pair.Server;
        var database = server.EntMan.GetComponent<TechnologyDatabaseComponent>(context.ResearchServer);
        if (allowed)
            Assert.That(database.UnlockedTechnologies, Does.Contain(context.Technology));
        else
            Assert.That(database.UnlockedTechnologies, Does.Not.Contain(context.Technology));
        Assert.That(server.EntMan.GetComponent<ResearchServerComponent>(context.ResearchServer).Points,
            Is.EqualTo(InitialPoints - (allowed ? context.Cost : 0)));
    }

    private static async Task WithConsole(string? cardPrototype, Func<ConsoleContext, Task> scenario)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var session = pair.Player!;
        var previousEntity = session.AttachedEntity;
        var ui = server.System<UserInterfaceSystem>();
        EntityUid console = default;
        EntityUid researchServer = default;
        EntityUid user = default;
        ProtoId<TechnologyPrototype> technology = default;
        var cost = 0;
        var equipped = cardPrototype == null;
        var opened = false;
        var completed = false;
        FishResearchConsoleMenu window = default!;
        try
        {
            await server.WaitPost(() =>
            {
                var research = server.System<ServerResearchSystem>();
                var power = server.System<PowerReceiverSystem>();
                researchServer = server.EntMan.SpawnEntity("ResearchAndDevelopmentServer", map.GridCoords);
                console = server.EntMan.SpawnEntity("ComputerResearchAndDevelopment", map.GridCoords);
                user = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
                power.SetPowerDisabled(researchServer, false);
                power.SetNeedsPower(researchServer, false);
                power.SetPowerDisabled(console, false);
                power.SetNeedsPower(console, false);
                research.RegisterClient(console, researchServer);
                var offered = research.GetAvailableTechnologies(researchServer)
                    .OrderBy(prototype => prototype.ID)
                    .First(prototype => prototype.Cost > 0 && prototype.RecipeUnlocks.Count > 0 && prototype.GenericUnlocks.Count == 0);
                technology = offered.ID;
                cost = offered.Cost;
                if (cardPrototype != null)
                {
                    var card = server.EntMan.SpawnEntity(cardPrototype, map.GridCoords);
                    equipped = server.System<InventorySystem>().TryEquip(user, card, "id", force: true);
                }
                server.PlayerMan.SetAttachedEntity(session, user);
                opened = ui.TryOpenUi(console, ResearchConsoleUiKey.Key, user);
                // Fish-edit: изменение очков публикует BUI-состояние только после открытия интерфейса.
                research.ModifyServerPoints(researchServer, InitialPoints);
            });
            await pair.RunTicksSync(15);
            await server.WaitAssertion(() =>
            {
                Assert.That(equipped, Is.True);
                Assert.That(opened, Is.True, "Research browsing must not require the director's ID.");
                Assert.That(server.System<PowerReceiverSystem>().IsPowered(console), Is.True);
                Assert.That(server.EntMan.GetComponent<ResearchServerComponent>(researchServer).Points, Is.EqualTo(InitialPoints));
                Assert.That(server.EntMan.GetComponent<TechnologyDatabaseComponent>(researchServer).UnlockedTechnologies,
                    Does.Not.Contain(technology));
            });
            await client.WaitPost(() =>
            {
                window = client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<FishResearchConsoleMenu>().Single();
                window.SelectTechnology(technology);
            });
            await client.WaitAssertion(() =>
            {
                var loc = client.ResolveDependency<ILocalizationManager>();
                Assert.That(window.FindControl<Label>("PointsLabel").Text,
                    Is.EqualTo(loc.GetString("fish-research-tree-points", ("points", InitialPoints))),
                    "The open console must receive the current point balance before checking purchase permissions.");
                Assert.That(window.SelectedTechnology, Is.EqualTo(technology));
            });
            await scenario(new ConsoleContext(pair, console, researchServer, user, technology, cost, window));
            completed = true;
        }
        finally
        {
            await server.WaitPost(() =>
            {
                if (server.EntMan.EntityExists(console))
                    ui.CloseUi(console, ResearchConsoleUiKey.Key);
                server.PlayerMan.SetAttachedEntity(session, previousEntity);
                server.EntMan.DeleteEntity(map.MapUid);
            });
            await pair.RunTicksSync(5);
            if (completed)
                await pair.CleanReturnAsync();
            else
                await pair.DisposeAsync();
        }
    }

    private sealed record ConsoleContext(TestPair Pair, EntityUid Console, EntityUid ResearchServer, EntityUid User,
        ProtoId<TechnologyPrototype> Technology, int Cost, FishResearchConsoleMenu Window);
}
