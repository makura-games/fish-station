using System.Linq;
using System.Numerics;
using Content.Client._Fish.Research.UI;
using Content.Client.Lathe;
using Content.Shared.Research.Components;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;

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
            var query = client.System<LatheSystem>().GetRecipeName("PowerCellHigh");
            var search = window.FindControl<LineEdit>("Search");
            search.SetText(query, invokeEvent: true);
            var results = window.FindControl<BoxContainer>("Results");
            Button SelectedLeaf()
            {
                var discipline = (Collapsible) results.GetChild(0);
                var tier = (Collapsible) discipline.Body!.GetChild(0).GetChild(0);
                Assert.That(discipline.BodyVisible && tier.BodyVisible, Is.True);
                var leaf = (Button) tier.Body!.GetChild(0).GetChild(0);
                Assert.That(leaf.Pressed, Is.True);
                return leaf;
            }

            Assert.That(SelectedLeaf().Text, Is.EqualTo(client.ResolveDependency<ILocalizationManager>().GetString("research-technology-advanced-powercells")));
            Assert.That(window.FindControl<BoxContainer>("Details").ChildCount, Is.Positive);

            window.Refresh(console, new ResearchConsoleBoundInterfaceState(7500));
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
            Assert.That(results.ChildCount, Is.Zero);
            client.EntMan.DeleteEntity(console);
        });
        await pair.CleanReturnAsync();
    }
}
