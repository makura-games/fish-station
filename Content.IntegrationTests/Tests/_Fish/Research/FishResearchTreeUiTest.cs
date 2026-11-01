using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client._Fish.Research.UI;
using Content.Client.Stylesheets.Palette;
using Content.IntegrationTests.Pair;
using Content.Server.Power.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Lathe;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Moq;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Input;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using ServerResearchSystem = Content.Server.Research.Systems.ResearchSystem;
using SpriteSystem = Robust.Client.GameObjects.SpriteSystem;

namespace Content.IntegrationTests.Tests._Fish.Research;

[TestFixture, NonParallelizable]
public sealed class FishResearchTreeUiTest
{
    private static readonly ProtoId<TechnologyPrototype> IndustrialRobotic = "IndustrialRobotic";
    private static readonly ProtoId<TechDisciplinePrototype> Experimental = "Experimental";

    [Test]
    public async Task ResearchConsoleOpensFishResearchTreeWindow()
    {
        await WithOpenConsole(context => context.Pair.Client.WaitAssertion(() =>
        {
            var ui = context.Pair.Client.ResolveDependency<IUserInterfaceManager>();
            Assert.That(ui.WindowRoot.Children.Any(control =>
                control.GetType().Name == "FishResearchConsoleMenu"), Is.True,
                "Opening the R&D console must display the Fish research tree window.");
        }));
    }

    /// <summary>The tree includes future and researched nodes while excluding unsupported hidden technologies.</summary>
    [Test]
    public async Task FullTreeShowsAllSupportedNonHiddenTechnologies()
    {
        await WithOpenConsole(async context =>
        {
            await context.Pair.Server.WaitPost(() => context.Pair.Server.System<ServerResearchSystem>()
                .AddTechnology(context.ResearchServer, "BasicRobotics"));
            await context.Pair.RunTicksSync(15);
            await context.Pair.Client.WaitAssertion(() =>
            {
                var client = context.Pair.Client;
                var database = client.EntMan.GetComponent<TechnologyDatabaseComponent>(context.Pair.ToClientUid(context.Console));
                var supported = new HashSet<ProtoId<TechDisciplinePrototype>>();
                foreach (var discipline in database.SupportedDisciplines)
                    supported.Add(discipline);
                var expected = client.ProtoMan.EnumeratePrototypes<TechnologyPrototype>()
                    .Where(technology => !technology.Hidden && supported.Contains(technology.Discipline))
                    .Select(technology => technology.ID).ToArray();

                Assert.That(database.UnlockedTechnologies, Does.Contain((ProtoId<TechnologyPrototype>) "BasicRobotics"));
                Assert.That(expected, Is.Not.Empty);
                Assert.That(VisibleIds(context.Window), Is.EquivalentTo(expected));
                Assert.That(VisibleIds(context.Window), Does.Contain("IndustrialRobotic"));
            });
        });
    }

    /// <summary>A title search retains the full cross-discipline prerequisite chain.</summary>
    [Test]
    public async Task TechnologySearchPreservesCrossDisciplinePrerequisites()
    {
        await WithOpenConsole(async context =>
        {
            var client = context.Pair.Client;
            await client.WaitPost(() =>
            {
                var title = client.ResolveDependency<ILocalizationManager>()
                    .GetString(client.ProtoMan.Index(IndustrialRobotic).Name);
                context.Window.FindControl<LineEdit>("SearchEdit").SetText($"  {title.ToUpperInvariant()}  ", invokeEvent: true);
            });
            await client.WaitAssertion(() =>
            {
                Assert.That(VisibleIds(context.Window), Is.EquivalentTo(PrerequisiteClosure(context.Pair, new[] { IndustrialRobotic })));
                Assert.That(VisibleIds(context.Window), Is.SupersetOf(new[]
                {
                    "IndustrialRobotic", "BasicRobotics", "IndustrialEngineering", "Durand",
                    "Gygax", "EnergyBarriers", "Ripley2", "RipleyAPLU",
                }));
            });
        });
    }

    /// <summary>Searching an unlocked recipe finds its technology and preserves same-tier prerequisites.</summary>
    [Test]
    public async Task RecipeSearchPreservesSameTierPrerequisites()
    {
        await WithOpenConsole(async context =>
        {
            var client = context.Pair.Client;
            await client.WaitPost(() =>
            {
                var recipe = client.System<SharedLatheSystem>().GetRecipeName((ProtoId<LatheRecipePrototype>) "ClarkeHarness");
                context.Window.FindControl<LineEdit>("SearchEdit").SetText(recipe, invokeEvent: true);
            });
            await client.WaitAssertion(() => Assert.That(VisibleIds(context.Window),
                Is.EquivalentTo(PrerequisiteClosure(context.Pair, new ProtoId<TechnologyPrototype>[] { "Clarke" }))));
        });
    }

    /// <summary>Category navigation retains required nodes from other research disciplines.</summary>
    [Test]
    public async Task DisciplineFilterPreservesExternalPrerequisites()
    {
        await WithOpenConsole(async context =>
        {
            var client = context.Pair.Client;
            Button disciplineButton = default!;
            await client.WaitPost(() =>
            {
                var title = client.ResolveDependency<ILocalizationManager>()
                    .GetString(client.ProtoMan.Index(Experimental).Name);
                disciplineButton = Descendants(context.Window.FindControl<BoxContainer>("DisciplineList"))
                    .OfType<Button>().Single(button => button.Text == title);
            });
            await Click(context.Pair, disciplineButton);
            await client.WaitAssertion(() =>
            {
                var roots = client.ProtoMan.EnumeratePrototypes<TechnologyPrototype>()
                    .Where(technology => !technology.Hidden && technology.Discipline == "Experimental")
                    .Select(technology => (ProtoId<TechnologyPrototype>) technology.ID);
                Assert.That(VisibleIds(context.Window), Is.EquivalentTo(PrerequisiteClosure(context.Pair, roots)));
                Assert.That(VisibleIds(context.Window), Is.SupersetOf(new[]
                    { "IndustrialEngineering", "Durand", "Gygax", "EnergyBarriers", "Ripley2", "RipleyAPLU" }));
            });
        });
    }

    /// <summary>All visible technologies display their dedicated purpose text in both supported locales.</summary>
    [TestCase("ru-RU")]
    [TestCase("en-US")]
    public async Task EveryTechnologyHasLocalizedPurposeInDetails(string culture)
    {
        await WithOpenConsole(async context =>
        {
            var client = context.Pair.Client;
            var snapshots = new List<(string Id, bool Found, bool Selected, string Purpose, string[] Captions)>();
            await client.WaitPost(() =>
            {
                var loc = client.ResolveDependency<ILocalizationManager>();
                foreach (var id in context.Window.Graph.VisibleTechnologyIds.ToArray())
                {
                    var technology = client.ProtoMan.Index(id);
                    var key = $"fish-{technology.Name.ToString().Replace('_', '-').ToLowerInvariant()}-description";
                    var found = loc.TryGetString(key, out var purpose);
                    var selected = context.Window.SelectTechnology(id);
                    snapshots.Add((id, found, selected, purpose, DetailCaptions(context.Window)));
                }
            });
            await client.WaitAssertion(() =>
            {
                Assert.That(snapshots.Select(snapshot => snapshot.Id), Is.EquivalentTo(ExpectedTechnologyIds(context)));
                Assert.Multiple(() =>
                {
                    foreach (var snapshot in snapshots)
                    {
                        Assert.That(snapshot.Found, Is.True, $"{culture}: missing purpose for {snapshot.Id}");
                        Assert.That(snapshot.Selected, Is.True, snapshot.Id);
                        Assert.That(snapshot.Captions, Does.Contain(snapshot.Purpose), $"{culture}: {snapshot.Id}");
                    }
                });
            });
        }, culture);
    }

    /// <summary>The selected node exposes cost, all direct requirements, and recipe icons.</summary>
    [Test]
    public async Task DetailsShowCostRequirementsAndRecipeIcons()
    {
        await WithOpenConsole(async context =>
        {
            var client = context.Pair.Client;
            await client.WaitPost(() => context.Window.SelectTechnology("IndustrialRobotic"));
            await client.WaitAssertion(() =>
            {
                var loc = client.ResolveDependency<ILocalizationManager>();
                var captions = DetailCaptions(context.Window);
                Assert.That(captions, Does.Contain(loc.GetString("fish-research-tree-cost", ("cost", 15000))));
                foreach (var prerequisite in new[] { "BasicRobotics", "IndustrialEngineering", "Durand" })
                {
                    var name = loc.GetString(client.ProtoMan.Index<TechnologyPrototype>(prerequisite).Name);
                    Assert.That(captions, Does.Contain(loc.GetString("fish-research-tree-prerequisite-missing", ("name", name))));
                }
                var recipe = client.System<SharedLatheSystem>()
                    .GetRecipeName((ProtoId<LatheRecipePrototype>) "ExosuitFabricatorHyperConvectionMachineCircuitboard");
                Assert.That(captions, Does.Contain(recipe));
                Assert.That(string.Join(" ", captions), Does.Not.Contain("75%"));
                Assert.That(Descendants(context.Window.DetailsContent).OfType<TextureRect>()
                    .Count(icon => icon.Texture != null), Is.GreaterThanOrEqualTo(2));
            });
        });
    }

    /// <summary>A real point balance update refreshes the header without moving or reselecting the tree.</summary>
    [Test]
    public async Task PointsUpdatePreservesSelectionViewportAndFilter()
    {
        await WithOpenConsole(async context =>
        {
            var client = context.Pair.Client;
            string[] visible = default!;
            await client.WaitPost(() =>
            {
                context.Window.Graph.SetFilter("Industrial", "");
                context.Window.SelectTechnology("Clarke");
                context.Window.Graph.SetViewport(1.25f, new Vector2(123, -45));
                visible = VisibleIds(context.Window);
            });
            await context.Pair.Server.WaitPost(() => context.Pair.Server.System<ServerResearchSystem>()
                .ModifyServerPoints(context.ResearchServer, 12345));
            await context.Pair.RunTicksSync(15);
            await client.WaitAssertion(() =>
            {
                var expected = client.ResolveDependency<ILocalizationManager>()
                    .GetString("fish-research-tree-points", ("points", 12345));
                Assert.That(context.Window.FindControl<Label>("PointsLabel").Text, Is.EqualTo(expected));
                Assert.That(context.Window.SelectedTechnology, Is.EqualTo((ProtoId<TechnologyPrototype>) "Clarke"));
                Assert.That(context.Window.Graph.Zoom, Is.EqualTo(1.25f));
                Assert.That(context.Window.Graph.Pan, Is.EqualTo(new Vector2(123, -45)));
                Assert.That(VisibleIds(context.Window), Is.EquivalentTo(visible));
            });
        });
    }

    /// <summary>Rendered dependency paths avoid unrelated cards, and a filtered chain occupies only its own ranks and lane.</summary>
    [Test]
    public async Task GraphRoutesAvoidCardsAndFilteredLayoutStaysCompact()
    {
        await WithOpenConsole(async context =>
        {
            var client = context.Pair.Client;
            var graph = context.Window.Graph;
            await client.WaitAssertion(() =>
            {
                Assert.That(graph.NodeBounds.Keys.Select(id => id.ToString()), Is.EquivalentTo(ExpectedTechnologyIds(context)));
                AssertRoutesAvoidCards(graph, "full tree");
            });
            await client.WaitPost(() => graph.SetFilter(Experimental, string.Empty));
            await client.WaitAssertion(() => AssertRoutesAvoidCards(graph, "Experimental"));
            await client.WaitPost(() =>
            {
                ProtoId<TechnologyPrototype> clarke = "Clarke";
                var title = client.ResolveDependency<ILocalizationManager>().GetString(client.ProtoMan.Index(clarke).Name);
                graph.SetFilter(null, title);
            });
            await client.WaitAssertion(() =>
            {
                var expected = PrerequisiteClosure(context.Pair, new ProtoId<TechnologyPrototype>[] { "Clarke" });
                Assert.That(graph.NodeBounds.Keys.Select(id => id.ToString()), Is.EquivalentTo(expected));
                var first = graph.NodeBounds["RipleyAPLU"];
                var middle = graph.NodeBounds["Ripley2"];
                var last = graph.NodeBounds["Clarke"];
                Assert.That(first.Right, Is.LessThan(middle.Left));
                Assert.That(middle.Right, Is.LessThan(last.Left));
                Assert.That(middle.Top, Is.EqualTo(first.Top).Within(0.01f));
                Assert.That(last.Top, Is.EqualTo(first.Top).Within(0.01f));
                Assert.That(graph.NodeBounds.Values.Min(bounds => bounds.Left), Is.InRange(0, first.Width));
                Assert.That(graph.NodeBounds.Values.Min(bounds => bounds.Top), Is.InRange(0, first.Height));
                Assert.That(graph.NodeBounds.Values.Max(bounds => bounds.Right),
                    Is.LessThanOrEqualTo(first.Width * expected.Count * 2));
                var edgeCount = expected.Sum(id => client.ProtoMan.Index<TechnologyPrototype>(id).TechnologyPrerequisites.Count);
                Assert.That(graph.Edges, Has.Count.EqualTo(edgeCount));
            });
        });
    }

    /// <summary>Selected connections retain their discipline color where inactive routes share segments and arrowheads.</summary>
    [TestCase("BasicRobotics")]
    [TestCase("IndustrialEngineering")]
    public async Task SelectedConnectionDrawsAfterOverlappingInactiveRoutes(string selectedId)
    {
        await WithOpenConsole(async context =>
        {
            var client = context.Pair.Client;
            var graph = context.Window.Graph;
            ProtoId<TechnologyPrototype> selected = selectedId;
            var draws = new List<(Vector2 From, Vector2 To, Color Color)>();
            FishResearchTreeEdge[] incoming = default!;
            var activeColor = default(Color);
            var inactiveColor = Palettes.Slate.HoveredElement;
            var commonFrom = Vector2.Zero;
            var commonTo = Vector2.Zero;
            var arrowFrom = Array.Empty<Vector2>();

            await client.WaitPost(() =>
            {
                graph.SetFilter(Experimental, string.Empty);
                graph.SelectTechnology(selected);
                graph.SetViewport(1.25f, new Vector2(17, 23));
                incoming = graph.Edges.Where(edge => edge.To == IndustrialRobotic).ToArray();
                activeColor = client.ProtoMan.Index(Experimental).Color;
                var points = incoming[0].Points;
                commonFrom = Physical(points[^2]);
                commonTo = Physical(points[^1]);
                arrowFrom = new[] { Physical(points[^1] + new Vector2(-7, -4)), Physical(points[^1] + new Vector2(-7, 4)) };
                var texture = client.System<SpriteSystem>().Frame0(client.ProtoMan.Index(IndustrialRobotic).Icon);
                var handle = new Mock<DrawingHandleScreen>(texture) { CallBase = true };
                handle.Setup(drawing => drawing.DrawLine(It.IsAny<Vector2>(), It.IsAny<Vector2>(), It.IsAny<Color>()))
                    .Callback<Vector2, Vector2, Color>((from, to, color) => draws.Add((from, to, color)));

                // Fish-edit: перехватываем команды настоящего Draw, включая порядок общих сегментов и стрелок.
                typeof(FishResearchTreeControl).GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!
                    .Invoke(graph, new object[] { handle.Object });
            });
            await client.WaitAssertion(() =>
            {
                Assert.That(incoming.Select(edge => edge.From.ToString()),
                    Is.EquivalentTo(new[] { "BasicRobotics", "IndustrialEngineering", "Durand" }));
                Assert.That(incoming.Count(edge => edge.From == selected), Is.EqualTo(1));
                foreach (var edge in incoming)
                {
                    Assert.That(Vector2.Distance(Physical(edge.Points[^2]), commonFrom), Is.LessThan(0.01f));
                    Assert.That(Vector2.Distance(Physical(edge.Points[^1]), commonTo), Is.LessThan(0.01f));
                }

                var sharedSegment = DrawsFor(commonFrom);
                var upperArrow = DrawsFor(arrowFrom[0]);
                var lowerArrow = DrawsFor(arrowFrom[1]);
                Assert.That(sharedSegment, Has.Length.EqualTo(3));
                Assert.That(sharedSegment.Count(draw => draw.Color == activeColor), Is.EqualTo(1));
                Assert.That(sharedSegment.Count(draw => draw.Color == inactiveColor), Is.EqualTo(2));
                Assert.That(upperArrow, Has.Length.EqualTo(3));
                Assert.That(lowerArrow, Has.Length.EqualTo(3));
                var firstActive = draws.FindIndex(draw => draw.Color == activeColor);
                Assert.That(firstActive, Is.GreaterThanOrEqualTo(0));
                Assert.Multiple(() =>
                {
                    Assert.That(sharedSegment[^1].Color, Is.EqualTo(activeColor),
                        "The last draw on a shared incoming segment must preserve the selected connection's color.");
                    Assert.That(upperArrow[^1].Color, Is.EqualTo(activeColor), "The shared upper arrowhead must remain highlighted.");
                    Assert.That(lowerArrow[^1].Color, Is.EqualTo(activeColor), "The shared lower arrowhead must remain highlighted.");
                    Assert.That(draws.Skip(firstActive).Where(draw => draw.Color == inactiveColor), Is.Empty,
                        "Inactive connections must be drawn before the active connection pass.");
                });
            });

            Vector2 Physical(Vector2 point) => (point * graph.Zoom + graph.Pan) * graph.UIScale;

            (Vector2 From, Vector2 To, Color Color)[] DrawsFor(Vector2 from) => draws.Where(draw =>
                Vector2.Distance(draw.From, from) < 0.01f && Vector2.Distance(draw.To, commonTo) < 0.01f).ToArray();
        });
    }

    /// <summary>A real transformed graph click selects its node and details without sending a research purchase.</summary>
    [Test]
    public async Task NodeClickWithPanAndZoomSelectsDetailsWithoutPurchase()
    {
        await WithOpenConsole(async context =>
        {
            var client = context.Pair.Client;
            var graph = context.Window.Graph;
            ProtoId<TechnologyPrototype> target = "Clarke";
            var requested = 0;
            var clickPosition = Vector2.Zero;
            await client.WaitPost(() =>
            {
                var title = client.ResolveDependency<ILocalizationManager>().GetString(client.ProtoMan.Index(target).Name);
                graph.SetFilter(null, title);
                context.Window.Measure(new Vector2(1100, 700));
                context.Window.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(1100, 700)));
                var center = graph.NodeBounds[target].Center;
                graph.SetViewport(1.25f, graph.Size / 2 - center * 1.25f);
                clickPosition = center * graph.Zoom + graph.Pan;
                context.Window.OnTechnologyCardPressed += _ => requested++;
            });
            await client.WaitAssertion(() =>
            {
                Assert.That(graph.Size.X, Is.GreaterThan(0));
                Assert.That(graph.Size.Y, Is.GreaterThan(0));
                Assert.That(graph.SizeBox.Contains(clickPosition), Is.True);
                Assert.That(context.Window.SelectedTechnology, Is.Not.EqualTo(target));
            });

            await Click(context.Pair, graph, clickPosition);
            await context.Pair.RunTicksSync(15);
            await client.WaitAssertion(() =>
            {
                var title = client.ResolveDependency<ILocalizationManager>().GetString(client.ProtoMan.Index(target).Name);
                Assert.That(context.Window.SelectedTechnology, Is.EqualTo(target));
                Assert.That(DetailCaptions(context.Window), Does.Contain(title));
                Assert.That(requested, Is.Zero);
            });
        });
    }

    /// <summary>Browsing an unavailable technology cannot issue a purchase through the research action.</summary>
    [TestCase("locked")]
    [TestCase("insufficient-points")]
    public async Task UnavailableSelectionCannotSendPurchase(string reason)
    {
        await WithOpenConsole(async context =>
        {
            var pair = context.Pair;
            var client = pair.Client;
            var server = pair.Server;
            var requested = 0;
            ProtoId<TechnologyPrototype> offered = default;
            ProtoId<TechnologyPrototype> target = default;
            await server.WaitPost(() => server.System<ServerResearchSystem>().ModifyServerPoints(context.ResearchServer, 100000));
            await pair.RunTicksSync(15);
            await client.WaitPost(() =>
            {
                var console = pair.ToClientUid(context.Console);
                var database = client.EntMan.GetComponent<TechnologyDatabaseComponent>(console);
                var cards = new HashSet<string>();
                foreach (var card in database.CurrentTechnologyCards)
                    cards.Add(card);
                offered = cards.First();
                context.Window.SelectTechnology(offered);
                target = reason switch
                {
                    "locked" => "IndustrialRobotic",
                    _ => offered,
                };
                context.Window.OnTechnologyCardPressed += _ => requested++;
            });
            await client.WaitAssertion(() => Assert.That(context.Window.ResearchButton.Disabled, Is.False,
                "An offered technology with enough points and valid research access must first be purchasable."));
            if (reason == "insufficient-points")
            {
                await server.WaitPost(() => server.System<ServerResearchSystem>().ModifyServerPoints(context.ResearchServer, -100000));
                await pair.RunTicksSync(15);
            }
            await client.WaitPost(() => context.Window.SelectTechnology(target));
            await client.WaitAssertion(() =>
            {
                var loc = client.ResolveDependency<ILocalizationManager>();
                var status = reason switch
                {
                    "locked" => loc.GetString("fish-research-tree-locked-reason"),
                    _ => loc.GetString("fish-research-tree-points-missing", ("points", client.ProtoMan.Index(offered).Cost)),
                };
                Assert.That(context.Window.ResearchButton.Disabled, Is.True);
                Assert.That(context.Window.PurchaseStatusLabel.Text, Does.Contain(status));
            });
            await Click(pair, context.Window.ResearchButton);
            await pair.RunTicksSync(15);
            await client.WaitAssertion(() => Assert.That(requested, Is.Zero));
            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.GetComponent<TechnologyDatabaseComponent>(context.ResearchServer).UnlockedTechnologies, Is.Empty);
                Assert.That(server.EntMan.GetComponent<ResearchServerComponent>(context.ResearchServer).Points,
                    Is.EqualTo(reason == "insufficient-points" ? 0 : 100000));
            });
        });
    }

    /// <summary>An offered technology is purchased once through the powered console's real BUI message path.</summary>
    [Test]
    public async Task OfferedTechnologyPurchaseUnlocksRecipesAndChargesPointsOnce()
    {
        await WithOpenConsole(async context =>
        {
            const int initialPoints = 100000;
            var pair = context.Pair;
            var server = pair.Server;
            var client = pair.Client;
            var power = server.System<PowerReceiverSystem>();
            ProtoId<TechnologyPrototype> target = default;
            ProtoId<LatheRecipePrototype>[] recipes = default!;
            var cost = 0;
            var requested = 0;

            await server.WaitPost(() =>
            {
                power.SetPowerDisabled(context.Console, false);
                power.SetPowerDisabled(context.ResearchServer, false);
                power.SetNeedsPower(context.Console, false);
                power.SetNeedsPower(context.ResearchServer, false);
                var database = server.EntMan.GetComponent<TechnologyDatabaseComponent>(context.ResearchServer);
                var offered = new List<TechnologyPrototype>();
                foreach (var id in database.CurrentTechnologyCards)
                    offered.Add(server.ProtoMan.Index<TechnologyPrototype>(id));
                var technology = offered.First(technology => technology.Cost > 0 &&
                    technology.RecipeUnlocks.Count > 0 && technology.GenericUnlocks.Count == 0);
                target = technology.ID;
                cost = technology.Cost;
                recipes = technology.RecipeUnlocks.ToArray();
                server.System<ServerResearchSystem>().ModifyServerPoints(context.ResearchServer, initialPoints);
            });
            await pair.RunTicksSync(15);
            await server.WaitAssertion(() =>
            {
                Assert.That(power.IsPowered(context.Console), Is.True);
                Assert.That(power.IsPowered(context.ResearchServer), Is.True);
                Assert.That(server.EntMan.GetComponent<TechnologyDatabaseComponent>(context.ResearchServer).UnlockedTechnologies, Is.Empty);
            });
            await client.WaitPost(() =>
            {
                context.Window.SelectTechnology(target);
                context.Window.OnTechnologyCardPressed += _ => requested++;
            });
            await client.WaitAssertion(() => Assert.That(context.Window.ResearchButton.Disabled, Is.False));

            await Click(pair, context.Window.ResearchButton);
            await pair.RunTicksSync(15);
            await server.WaitAssertion(() =>
            {
                var database = server.EntMan.GetComponent<TechnologyDatabaseComponent>(context.ResearchServer);
                var consoleDatabase = server.EntMan.GetComponent<TechnologyDatabaseComponent>(context.Console);
                Assert.That(database.UnlockedTechnologies, Does.Contain(target));
                Assert.That(consoleDatabase.UnlockedTechnologies, Does.Contain(target));
                Assert.That(database.UnlockedRecipes, Is.SupersetOf(recipes));
                Assert.That(consoleDatabase.UnlockedRecipes, Is.SupersetOf(recipes));
                Assert.That(server.EntMan.GetComponent<ResearchServerComponent>(context.ResearchServer).Points,
                    Is.EqualTo(initialPoints - cost));
            });
            await client.WaitAssertion(() =>
            {
                var database = client.EntMan.GetComponent<TechnologyDatabaseComponent>(pair.ToClientUid(context.Console));
                var loc = client.ResolveDependency<ILocalizationManager>();
                Assert.That(database.UnlockedTechnologies, Does.Contain(target));
                Assert.That(database.UnlockedRecipes, Is.SupersetOf(recipes));
                Assert.That(requested, Is.EqualTo(1));
                Assert.That(context.Window.SelectedTechnology, Is.EqualTo(target));
                Assert.That(context.Window.ResearchButton.Disabled, Is.True);
                Assert.That(context.Window.PurchaseStatusLabel.Text, Does.Contain(loc.GetString("fish-research-tree-already-researched")));
                Assert.That(context.Window.FindControl<Label>("PointsLabel").Text,
                    Is.EqualTo(loc.GetString("fish-research-tree-points", ("points", initialPoints - cost))));
            });

            await Click(pair, context.Window.ResearchButton);
            await pair.RunTicksSync(15);
            await client.WaitAssertion(() => Assert.That(requested, Is.EqualTo(1)));
            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.GetComponent<TechnologyDatabaseComponent>(context.ResearchServer).UnlockedTechnologies,
                    Has.Count.EqualTo(1));
                Assert.That(server.EntMan.GetComponent<ResearchServerComponent>(context.ResearchServer).Points,
                    Is.EqualTo(initialPoints - cost));
            });
        });
    }

    /// <summary>A stale legacy choice list cannot prevent an eligible purchase through the real BUI path.</summary>
    [Test]
    public async Task EligibleTechnologyCanBePurchasedWhenLegacyChoiceListIsEmpty()
    {
        await WithOpenConsole(async context =>
        {
            var pair = context.Pair;
            var server = pair.Server;
            var client = pair.Client;
            var research = server.System<ServerResearchSystem>();
            EntityUid staleServer = default;
            ProtoId<TechnologyPrototype> target = default;
            var cost = 0;
            try
            {
                await server.WaitPost(() =>
                {
                    var power = server.System<PowerReceiverSystem>();
                    power.SetPowerDisabled(context.Console, false);
                    power.SetPowerDisabled(context.ResearchServer, false);
                    power.SetNeedsPower(context.Console, false);
                    power.SetNeedsPower(context.ResearchServer, false);
                    var technology = research.GetAvailableTechnologies(context.ResearchServer)
                        .First(technology => technology.GenericUnlocks.Count == 0);
                    target = technology.ID;
                    cost = technology.Cost;
                    research.ModifyServerPoints(context.ResearchServer, 100000);
                    staleServer = server.EntMan.Spawn("ResearchAndDevelopmentServer", doMapInit: false);
                    research.Sync(context.Console, staleServer);
                });
                await pair.RunUntilSynced();
                await client.WaitPost(() => context.Window.SelectTechnology(target));
                await client.WaitAssertion(() =>
                {
                    var database = client.EntMan.GetComponent<TechnologyDatabaseComponent>(pair.ToClientUid(context.Console));
                    Assert.That(database.CurrentTechnologyCards, Is.Empty);
                    Assert.That(context.Window.ResearchButton.Disabled, Is.False,
                        "An eligible technology must remain purchasable when it is absent from the old card list.");
                });
                await Click(pair, context.Window.ResearchButton);
                await pair.RunUntilSynced();
                await server.WaitAssertion(() =>
                {
                    Assert.That(server.EntMan.GetComponent<TechnologyDatabaseComponent>(context.ResearchServer).UnlockedTechnologies,
                        Does.Contain(target));
                    Assert.That(server.EntMan.GetComponent<ResearchServerComponent>(context.ResearchServer).Points,
                        Is.EqualTo(100000 - cost));
                });
            }
            finally
            {
                await server.WaitPost(() =>
                {
                    if (server.EntMan.EntityExists(staleServer))
                        server.EntMan.DeleteEntity(staleServer);
                });
            }
        });
    }

    /// <summary>A purchase refreshes the researched state regardless of network component update order.</summary>
    [TestCase(false)]
    [TestCase(true)]
    public async Task PurchaseRefreshesImmediatelyForEitherNetworkComponentOrder(bool databaseFirst)
    {
        await WithOpenConsole(async context =>
        {
            var pair = context.Pair;
            var server = pair.Server;
            var client = pair.Client;
            var research = server.System<ServerResearchSystem>();
            ProtoId<TechnologyPrototype> source = "SurgeryTech";
            ProtoId<TechnologyPrototype> reward = "AdvancedCleaning";
            ProtoId<TechnologyPrototype> next = "SurgeryTechAdvanced";
            var sourcePrototype = server.ProtoMan.Index(source);
            var rewardPrototype = server.ProtoMan.Index(reward);
            var rewardCost = rewardPrototype.Cost;
            var recipes = sourcePrototype.RecipeUnlocks.ToArray();
            IComponentState databaseState = default!;
            IComponentState interfaceState = default!;
            var purchased = false;

            await server.WaitPost(() =>
            {
                research.AddTechnology(context.ResearchServer, "BasicRobotics");
                var points = server.EntMan.GetComponent<ResearchServerComponent>(context.ResearchServer).Points;
                research.ModifyServerPoints(context.ResearchServer, sourcePrototype.Cost + rewardCost - points);
            });
            await pair.RunUntilSynced();
            await client.WaitPost(() =>
            {
                context.Window.SelectTechnology(source);
                context.Window.Graph.SetViewport(1.25f, new Vector2(123, -45));
            });
            await client.WaitAssertion(() =>
            {
                Assert.That(context.Window.ResearchButton.Disabled, Is.False);
                Assert.That(client.EntMan.GetComponent<TechnologyDatabaseComponent>(pair.ToClientUid(context.Console))
                    .UnlockedTechnologies, Does.Not.Contain(source));
            });

            await server.WaitPost(() =>
            {
                purchased = research.UnlockTechnology(context.Console, sourcePrototype, pair.Player!.AttachedEntity!.Value);
                var database = server.EntMan.GetComponent<TechnologyDatabaseComponent>(context.Console);
                var userInterface = server.EntMan.GetComponent<UserInterfaceComponent>(context.Console);
                databaseState = server.EntMan.GetComponentState(server.EntMan.EventBus, database, null, GameTick.Zero);
                interfaceState = server.EntMan.GetComponentState(server.EntMan.EventBus, userInterface, null, GameTick.Zero);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(purchased, Is.True);
                Assert.That(databaseState, Is.Not.Null);
                Assert.That(interfaceState, Is.Not.Null);
            });

            // Fish-edit: применяем настоящие сетевые снимки в заданном порядке без следующего тика или обновления BUI.
            await client.WaitPost(() =>
            {
                var console = pair.ToClientUid(context.Console);
                var database = client.EntMan.GetComponent<TechnologyDatabaseComponent>(console);
                var userInterface = client.EntMan.GetComponent<UserInterfaceComponent>(console);
                var databaseEvent = new ComponentHandleState(databaseState, null);
                var interfaceEvent = new ComponentHandleState(interfaceState, null);
                if (databaseFirst)
                    client.EntMan.EventBus.RaiseComponentEvent(console, database, ref databaseEvent);
                client.EntMan.EventBus.RaiseComponentEvent(console, userInterface, ref interfaceEvent);
                if (!databaseFirst)
                    client.EntMan.EventBus.RaiseComponentEvent(console, database, ref databaseEvent);
            });
            await client.WaitAssertion(() =>
            {
                var database = client.EntMan.GetComponent<TechnologyDatabaseComponent>(pair.ToClientUid(context.Console));
                var loc = client.ResolveDependency<ILocalizationManager>();
                Assert.That(database.UnlockedTechnologies, Does.Contain(source));
                Assert.That(database.UnlockedRecipes, Is.SupersetOf(recipes));
                Assert.Multiple(() =>
                {
                    Assert.That(context.Window.ResearchButton.Disabled, Is.True,
                        "The purchase must disable its action immediately, without another technology purchase.");
                    Assert.That(context.Window.PurchaseStatusLabel.Text,
                        Does.Contain(loc.GetString("fish-research-tree-already-researched")));
                    Assert.That(context.Window.SelectedTechnology, Is.EqualTo(source));
                    Assert.That(context.Window.Graph.Zoom, Is.EqualTo(1.25f));
                    Assert.That(context.Window.Graph.Pan, Is.EqualTo(new Vector2(123, -45)));
                });
            });
            await client.WaitPost(() => context.Window.SelectTechnology(reward));
            await client.WaitAssertion(() =>
            {
                var loc = client.ResolveDependency<ILocalizationManager>();
                Assert.That(DetailCaptions(context.Window),
                    Does.Contain(loc.GetString("fish-research-tree-cost", ("cost", rewardCost))));
                Assert.That(context.Window.ResearchButton.Disabled, Is.False);
            });
            await client.WaitPost(() => context.Window.SelectTechnology(next));
            await client.WaitAssertion(() => Assert.That(context.Window.ResearchButton.Disabled, Is.False,
                "Research whose prerequisite was just purchased must become available in the same state update."));
        });
    }

    private static string[] ExpectedTechnologyIds(ConsoleContext context)
    {
        var client = context.Pair.Client;
        var database = client.EntMan.GetComponent<TechnologyDatabaseComponent>(context.Pair.ToClientUid(context.Console));
        var supported = new HashSet<ProtoId<TechDisciplinePrototype>>();
        foreach (var discipline in database.SupportedDisciplines)
            supported.Add(discipline);
        return client.ProtoMan.EnumeratePrototypes<TechnologyPrototype>()
            .Where(technology => !technology.Hidden && supported.Contains(technology.Discipline))
            .Select(technology => technology.ID).ToArray();
    }

    private static HashSet<string> PrerequisiteClosure(TestPair pair, IEnumerable<ProtoId<TechnologyPrototype>> roots)
    {
        var result = new HashSet<string>();
        var pending = new Stack<ProtoId<TechnologyPrototype>>(roots);
        while (pending.TryPop(out var id))
        {
            if (!result.Add(id))
                continue;
            foreach (var prerequisite in pair.Client.ProtoMan.Index(id).TechnologyPrerequisites)
                pending.Push(prerequisite);
        }
        return result;
    }

    private static async Task WithOpenConsole(Func<ConsoleContext, Task> scenario, string culture = null)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var entMan = server.EntMan;
        var ui = server.System<UserInterfaceSystem>();
        var uiMan = client.ResolveDependency<IUserInterfaceManager>();
        var map = await pair.CreateTestMap();
        var session = pair.Player!;
        var previousEntity = session.AttachedEntity;
        var loc = client.ResolveDependency<ILocalizationManager>();
        var previousCulture = loc.DefaultCulture;
        FishResearchConsoleMenu window = default!;
        EntityUid researchServer = default;
        EntityUid console = default;
        EntityUid user = default;
        var opened = false;
        var equipped = false;
        var completed = false;

        try
        {
            if (culture != null)
                await client.WaitPost(() => loc.SetCulture(CultureInfo.GetCultureInfo(culture)));
            await server.WaitPost(() =>
            {
                researchServer = entMan.SpawnEntity("ResearchAndDevelopmentServer", map.GridCoords);
                console = entMan.SpawnEntity("ComputerResearchAndDevelopment", map.GridCoords);
                user = entMan.SpawnEntity("MobHuman", map.GridCoords);
                var card = entMan.SpawnEntity("RDIDCard", map.GridCoords);
                equipped = server.System<InventorySystem>().TryEquip(user, card, "id", force: true);
                server.PlayerMan.SetAttachedEntity(session, user);
                opened = ui.TryOpenUi(console, ResearchConsoleUiKey.Key, user);
            });

            await pair.RunTicksSync(15);

            await server.WaitAssertion(() =>
            {
                Assert.That(equipped, Is.True);
                Assert.That(opened, Is.True);
            });
            await client.WaitPost(() => window = uiMan.WindowRoot.Children.OfType<FishResearchConsoleMenu>().Single());
            await scenario(new ConsoleContext(pair, console, researchServer, window));
            completed = true;
        }
        finally
        {
            await server.WaitPost(() =>
            {
                if (entMan.EntityExists(console))
                    ui.CloseUi(console, ResearchConsoleUiKey.Key);

                server.PlayerMan.SetAttachedEntity(session, previousEntity);
                entMan.DeleteEntity(map.MapUid);
            });
            await pair.RunTicksSync(5);
            if (culture != null)
                await client.WaitPost(() => loc.DefaultCulture = previousCulture);
            if (completed)
                await pair.CleanReturnAsync();
            else
                await pair.DisposeAsync();
        }
    }

    private static string[] VisibleIds(FishResearchConsoleMenu window) =>
        window.Graph.VisibleTechnologyIds.Select(id => id.ToString()).ToArray();

    private static string[] DetailCaptions(FishResearchConsoleMenu window) =>
        Descendants(window.DetailsContent).OfType<RichTextLabel>()
            .Select(label => FormattedMessage.RemoveMarkupOrThrow(label.Text ?? string.Empty)).ToArray();

    private static void AssertRoutesAvoidCards(FishResearchTreeControl graph, string view)
    {
        Assert.That(graph.NodeBounds.Keys.Select(id => id.ToString()), Does.Contain("Gygax"), view);
        Assert.That(graph.Edges.Any(edge => edge.From == "IndustrialEngineering" && edge.To == "IndustrialRobotic"), Is.True, view);
        Assert.That(graph.Edges.Any(edge => edge.From == "Ripley2" && edge.To == "Gygax"), Is.True, view);
        var intersections = new List<string>();
        foreach (var edge in graph.Edges)
        {
            Assert.That(edge.Points.Count, Is.GreaterThanOrEqualTo(2), $"{view}: {edge.From} -> {edge.To}");
            Assert.That(graph.NodeBounds[edge.From].Right, Is.LessThan(graph.NodeBounds[edge.To].Left), view);
            for (var index = 1; index < edge.Points.Count; index++)
            {
                var from = edge.Points[index - 1];
                var to = edge.Points[index];
                Assert.That(from.X == to.X || from.Y == to.Y, Is.True,
                    $"{view}: route segment must be axis aligned: {edge.From} -> {edge.To}");
                var minimum = Vector2.Min(from, to);
                var maximum = Vector2.Max(from, to);
                foreach (var (id, bounds) in graph.NodeBounds)
                {
                    if (id == edge.From || id == edge.To)
                        continue;
                    if (minimum.X <= bounds.Right && maximum.X >= bounds.Left &&
                        minimum.Y <= bounds.Bottom && maximum.Y >= bounds.Top)
                        intersections.Add($"{edge.From} -> {edge.To}, segment {index}, crosses {id}");
                }
            }
        }
        Assert.That(intersections, Is.Empty, view);
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (var child in root.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static async Task Click(TestPair pair, Control control, Vector2? relativePosition = null)
    {
        var position = relativePosition ?? control.Size / 2;
        var pixelPosition = position * control.UIScale;
        var screen = new ScreenCoordinates(control.GlobalPixelPosition + pixelPosition, control.Window?.Id ?? default);
        await pair.Client.DoGuiEvent(control, new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick,
            BoundKeyState.Down, screen, default, position, pixelPosition));
        await pair.RunTicksSync(1);
        await pair.Client.DoGuiEvent(control, new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick,
            BoundKeyState.Up, screen, default, position, pixelPosition));
        await pair.RunTicksSync(1);
    }

    private sealed record ConsoleContext(TestPair Pair, EntityUid Console, EntityUid ResearchServer, FishResearchConsoleMenu Window);
}
