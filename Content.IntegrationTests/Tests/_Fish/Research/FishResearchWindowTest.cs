using System.Linq;
using System.Numerics;
using Content.Client._Fish.Research.UI;
using Content.Client.Lathe;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.Research;

[TestFixture]
public sealed class FishResearchWindowTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: FishTestResearchCatalog
          suffix: Fish
          components:
          - type: TechnologyDatabase
            supportedDisciplines: [Industrial]
            currentTechnologyCards: [AdvancedPowercells]
        """;

    [Test]
    public async Task OffersAndCatalogUseTheSameTechnologyData()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var console = client.EntMan.SpawnEntity("FishTestResearchCatalog", MapCoordinates.Nullspace);
            var window = new FishResearchWindow();
            window.Refresh(console, new ResearchConsoleBoundInterfaceState(0));
            window.Measure(new Vector2(790, 580));
            window.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(790, 580)));
            var card = (FishTechnologyCard) window.FindControl<BoxContainer>("CardRows").GetChild(0).GetChild(0);
            Assert.That(card.FindControl<TextureRect>("Icon").Texture, Is.Not.Null);
            Assert.That(card.FindControl<BoxContainer>("Preview").ChildCount, Is.Positive);
            var buy = card.FindControl<Button>("ResearchButton");
            Assert.That(buy.Disabled, Is.True);
            var catalog = window.FindControl<BoxContainer>("Results");
            var firstBranch = (Collapsible) catalog.GetChild(0);
            var firstLevel = (Collapsible) firstBranch.Body!.GetChild(0).GetChild(0);
            var leaves = firstLevel.Body!.GetChild(0).Children.OfType<Button>().ToArray();
            var first = leaves.Single(leaf => leaf.Pressed);
            var next = leaves.First(leaf => !leaf.Pressed);
            next.Pressed = true;
            var accent = client.ResolveDependency<IPrototypeManager>().Index<TechDisciplinePrototype>("Industrial").Color;
            Assert.That(((StyleBoxFlat) next.StyleBoxOverride!).BorderColor, Is.EqualTo(accent));
            Assert.That(first.StyleBoxOverride, Is.Null);
            var query = client.System<LatheSystem>().GetRecipeName("PowerCellHigh");
            var search = window.FindControl<LineEdit>("Search");
            search.SetText(query, invokeEvent: true);
            Button SelectedLeaf()
            {
                var discipline = (Collapsible) catalog.GetChild(0);
                var tier = (Collapsible) discipline.Body!.GetChild(0).GetChild(0);
                Assert.That(discipline.BodyVisible && tier.BodyVisible, Is.True);
                var leaf = (Button) tier.Body!.GetChild(0).GetChild(0);
                Assert.That(leaf.Pressed, Is.True);
                Assert.That(((StyleBoxFlat) leaf.StyleBoxOverride!).BorderColor, Is.EqualTo(accent));
                return leaf;
            }

            Assert.That(SelectedLeaf().Text, Is.EqualTo(client.ResolveDependency<ILocalizationManager>().GetString("research-technology-advanced-powercells")));
            Assert.That(window.FindControl<BoxContainer>("Details").ChildCount, Is.Positive);

            var offer = client.EntMan.GetComponent<TechnologyDatabaseComponent>(console).CurrentTechnologyCards.Single();
            var price = client.ResolveDependency<IPrototypeManager>().Index<TechnologyPrototype>(offer).Cost;
            window.Refresh(console, new ResearchConsoleBoundInterfaceState(price));
            Assert.That(buy.Disabled, Is.False);
            Assert.That(search.Text, Is.EqualTo(query));
            var gyroscope = client.System<LatheSystem>().GetRecipeName("GyroscopeMachineCircuitboard");
            search.SetText(gyroscope, invokeEvent: true);
            Assert.That(SelectedLeaf().Text, Is.EqualTo(client.ResolveDependency<ILocalizationManager>().GetString("research-technology-shuttlecraft")));
            var details = window.FindControl<BoxContainer>("Details");
            Assert.That(details.Children.OfType<BoxContainer>()
                .SelectMany(row => row.Children.OfType<RichTextLabel>())
                .Any(label => label.GetMessage()?.Contains(gyroscope) == true), Is.True);
            Assert.That(window.FindControl<BoxContainer>("CardRows").GetChild(0).Children.OfType<FishTechnologyCard>().Single(),
                Is.SameAs(card));
            search.SetText("xxxxxxxxxxxxxx", invokeEvent: true);
            Assert.That(catalog.ChildCount, Is.Zero);
            client.EntMan.DeleteEntity(console);
        });
        await pair.CleanReturnAsync();
    }
}
