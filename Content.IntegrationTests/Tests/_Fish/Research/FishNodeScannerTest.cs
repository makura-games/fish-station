#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Xenoarchaeology.Ui;
using Content.IntegrationTests.Pair;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.NameIdentifier;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Content.Shared.Xenoarchaeology.Equipment.Components;
using Robust.Client.GameObjects;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Fish.Research;

[TestFixture, NonParallelizable]
public sealed class FishNodeScannerTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: FishNodeScannerTestNode
          name: test artifact node
          description: A concealed artifact effect.
          suffix: Fish
          components:
          - type: XenoArtifactNode
            triggerTip: artifact-trigger-hint-health-analyzer
            maxDurability: 3
            maxDurabilityCanDecreaseBy:
              min: 0
              max: 0
        """;

    [Test]
    public async Task HeldScanUsesConsoleDetailsAndRefreshesLockedNodeInformation()
    {
        await WithScanner(async context =>
        {
            var pair = context.Pair;
            var client = pair.Client;
            var loc = client.ResolveDependency<ILocalizationManager>();
            var window = context.Window;
            await client.WaitPost(() => window.SetSelectedNode(ClientNode(context, context.ChildNode)));
            await client.WaitAssertion(() =>
            {
                Assert.That(window.Title, Is.EqualTo(loc.GetString("node-scan-display-title")));
                Assert.That(window.FindControl<Button>("ExtractButton").Visible, Is.False);
                Assert.That(window.FindControl<Button>("ServerButton").Visible, Is.False);
                Assert.That(window.FindControl<XenoArtifactGraphControl>("GraphControl").Visible, Is.True);
                Assert.That(window.FindControl<BoxContainer>("InfoContainer").Visible, Is.True);
                var hidden = loc.GetString("analysis-console-info-effect-value", ("state", false));
                Assert.That(window.FindControl<RichTextLabel>("EffectValueLabel").Text, Is.EqualTo(hidden));
                Assert.That(window.FindControl<RichTextLabel>("TriggerValueLabel").Text, Is.EqualTo(hidden));
            });

            await pair.Server.WaitPost(() =>
            {
                var artifacts = pair.Server.System<SharedXenoArtifactSystem>();
                artifacts.SetNodeUnlocked((context.RootNode, null));
                artifacts.SetNodeDurability((context.ChildNode, null), 1);
            });
            // Частота обновления штатного сканера: одна секунда, плюс запас на доставку состояния.
            await pair.RunTicksSync(90);
            await client.WaitAssertion(() =>
            {
                var description = client.EntMan.GetComponent<MetaDataComponent>(pair.ToClientUid(context.ChildNode)).EntityDescription;
                Assert.That(window.FindControl<RichTextLabel>("EffectValueLabel").Text,
                    Is.EqualTo(loc.GetString("analysis-console-info-effect-value", ("state", true), ("info", description))));
                Assert.That(window.FindControl<RichTextLabel>("TriggerValueLabel").Text,
                    Does.Contain(loc.GetString("artifact-trigger-hint-health-analyzer")));
                Assert.That(window.FindControl<RichTextLabel>("DurabilityValueLabel").Text,
                    Is.EqualTo(FormattedMessage.FromMarkupOrThrow(loc.GetString("analysis-console-info-durability-value",
                        ("color", Color.Red), ("current", 1), ("max", 3))).ToMarkup()));
            });
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(pair.Server.EntMan.GetComponent<XenoArtifactNodeComponent>(context.ChildNode).ConsumedResearchValue, Is.Zero);
                Assert.That(pair.Server.EntMan.HasComponent<AnalysisConsoleComponent>(context.Scanner), Is.False);
            });
        });
    }

    [Test]
    public async Task ScannerShowsTriggeredIdsWithoutRevealingLockedNodeInformation()
    {
        await WithScanner(async context =>
        {
            var pair = context.Pair;
            var server = pair.Server;
            var client = pair.Client;
            var loc = client.ResolveDependency<ILocalizationManager>();
            var artifacts = server.System<SharedXenoArtifactSystem>();
            await server.WaitPost(() =>
            {
                var identifier = server.EntMan.EnsureComponent<NameIdentifierComponent>(context.RootNode);
                identifier.Identifier = 123;
                server.EntMan.Dirty(context.RootNode, identifier);
                artifacts.TriggerXenoArtifact(
                    (context.Artifact, server.EntMan.GetComponent<XenoArtifactComponent>(context.Artifact)),
                    (context.RootNode, server.EntMan.GetComponent<XenoArtifactNodeComponent>(context.RootNode)));
            });
            await pair.RunTicksSync(90);
            await client.WaitPost(() => context.Window.SetSelectedNode(ClientNode(context, context.ChildNode)));
            await client.WaitAssertion(() =>
            {
                Assert.That(ScannerState(context.Window).Text,
                    Is.EqualTo($"{loc.GetString("node-scanner-artifact-state-unlocking")}\n123"));
                var hidden = loc.GetString("analysis-console-info-effect-value", ("state", false));
                Assert.That(context.Window.FindControl<RichTextLabel>("EffectValueLabel").Text, Is.EqualTo(hidden));
                Assert.That(context.Window.FindControl<RichTextLabel>("TriggerValueLabel").Text, Is.EqualTo(hidden));
            });
            await server.WaitPost(() => artifacts.CancelUnlockingState(
                (context.Artifact, server.EntMan.GetComponent<XenoArtifactUnlockingComponent>(context.Artifact),
                    server.EntMan.GetComponent<XenoArtifactComponent>(context.Artifact))));
            await pair.RunTicksSync(90);
            await client.WaitAssertion(() => Assert.That(ScannerState(context.Window).Text,
                Is.EqualTo(loc.GetString("node-scanner-artifact-state-ready"))));
        });
    }

    [Test]
    public async Task ScannerCannotProcessConsoleExtractionMessages()
    {
        await WithScanner(async context =>
        {
            var server = context.Pair.Server;
            var artifacts = server.System<SharedXenoArtifactSystem>();
            await server.WaitPost(() =>
            {
                artifacts.SetNodeUnlocked((context.RootNode, null));
                artifacts.SetNodeDurability((context.RootNode, null), 1);
            });
            await server.WaitAssertion(() => Assert.That(artifacts.GetResearchValue(
                (context.RootNode, server.EntMan.GetComponent<XenoArtifactNodeComponent>(context.RootNode))),
                Is.GreaterThan(0)));
            await server.WaitPost(() => server.System<Robust.Server.GameObjects.UserInterfaceSystem>().RaiseUiMessage(
                context.Scanner, NodeScannerUiKey.Key,
                new AnalysisConsoleExtractButtonPressedMessage { Actor = context.User }));
            await server.WaitAssertion(() => Assert.That(
                server.EntMan.GetComponent<XenoArtifactNodeComponent>(context.RootNode).ConsumedResearchValue, Is.Zero));
        });
    }

    [Test]
    public async Task RelinkingToEmptyArtifactClearsSelectionAndRejectsPreviousNode()
    {
        await WithScanner(async context =>
        {
            var pair = context.Pair;
            var client = pair.Client;
            var loc = client.ResolveDependency<ILocalizationManager>();
            EntityUid emptyArtifact = default;
            await client.WaitPost(() => context.Window.SetSelectedNode(ClientNode(context, context.RootNode)));
            await pair.RunTicksSync(90);
            await pair.Server.WaitPost(() =>
            {
                var coordinates = pair.Server.EntMan.GetComponent<TransformComponent>(context.Artifact).Coordinates;
                emptyArtifact = pair.Server.EntMan.SpawnEntity("DummyArtifactStructure", coordinates);
            });
            await pair.RunTicksSync(5);
            await Scan(pair, emptyArtifact);
            await pair.Server.WaitAssertion(() => Assert.That(
                pair.Server.EntMan.GetComponent<NodeScannerConnectedComponent>(context.Scanner).AttachedTo,
                Is.EqualTo(emptyArtifact)));
            await client.WaitPost(() => context.Window.SetSelectedNode(ClientNode(context, context.RootNode)));
            await client.WaitAssertion(() =>
            {
                Assert.That(context.Window.FindControl<BoxContainer>("InfoContainer").Visible, Is.False);
                var empty = context.Window.FindControl<Label>("NoArtiLabel");
                Assert.That(empty.Visible, Is.True);
                Assert.That(empty.Text, Is.EqualTo(loc.GetString("node-scan-no-data")));
            });
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LosingOrDeletingArtifactDisconnectsAndClearsDetails(bool delete)
    {
        await WithScanner(async context =>
        {
            var pair = context.Pair;
            var client = pair.Client;
            var loc = client.ResolveDependency<ILocalizationManager>();
            await client.WaitPost(() => context.Window.SetSelectedNode(ClientNode(context, context.RootNode)));
            await pair.Server.WaitPost(() =>
            {
                if (delete)
                    pair.Server.EntMan.DeleteEntity(context.Artifact);
                else
                {
                    var coordinates = pair.Server.EntMan.GetComponent<TransformComponent>(context.Artifact).Coordinates;
                    pair.Server.System<SharedTransformSystem>().SetCoordinates(context.Artifact,
                        new EntityCoordinates(coordinates.EntityId, coordinates.Position + new Vector2(6, 0)));
                }
            });
            await pair.RunTicksSync(90);
            await pair.Server.WaitAssertion(() =>
                Assert.That(pair.Server.EntMan.HasComponent<NodeScannerConnectedComponent>(context.Scanner), Is.False));
            await client.WaitAssertion(() =>
            {
                Assert.That(context.Window.FindControl<BoxContainer>("InfoContainer").Visible, Is.False);
                Assert.That(context.Window.FindControl<Label>("NoneSelectedLabel").Visible, Is.False);
                var empty = context.Window.FindControl<Label>("NoArtiLabel");
                Assert.That(empty.Visible, Is.True);
                Assert.That(empty.Text, Is.EqualTo(loc.GetString("node-scanner-artifact-non-connected")));
                Assert.That(ScannerState(context.Window).Text, Is.Empty);
            });
        });
    }

    [TestCase(820, 500)]
    [TestCase(700, 350)]
    public async Task ScannerFitsNativeCompactWindow(int width, int height)
    {
        await WithScanner(async context =>
        {
            var window = context.Window;
            await context.Pair.Client.WaitPost(() =>
            {
                window.SetSelectedNode(ClientNode(context, context.RootNode));
                window.SetSize = new Vector2(width, height);
                window.InvalidateMeasure();
                window.InvalidateArrange();
                window.Measure(window.SetSize);
                window.Arrange(UIBox2.FromDimensions(window.Position, window.SetSize));
            });
            await context.Pair.Client.WaitAssertion(() =>
            {
                Assert.That(window.Width, Is.LessThanOrEqualTo(width));
                Assert.That(window.Height, Is.LessThanOrEqualTo(height));
                foreach (var control in new Control[]
                         {
                             window.FindControl<XenoArtifactGraphControl>("GraphControl"),
                             ScannerState(window),
                             window.FindControl<PanelContainer>("BackPanel"),
                         })
                {
                    var name = control.Name;
                    var position = control.GlobalPosition - window.GlobalPosition;
                    Assert.That(position.X, Is.GreaterThanOrEqualTo(-1), name);
                    Assert.That(position.Y, Is.GreaterThanOrEqualTo(-1), name);
                    Assert.That(position.X + control.Width, Is.LessThanOrEqualTo(window.Width + 1), name);
                    Assert.That(position.Y + control.Height, Is.LessThanOrEqualTo(window.Height + 1), name);
                }
                Assert.That(window.FindControl<XenoArtifactGraphControl>("GraphControl").Width, Is.GreaterThan(250));
            });
        });
    }

    private static Entity<XenoArtifactNodeComponent> ClientNode(ScannerContext context, EntityUid serverNode)
    {
        var uid = context.Pair.ToClientUid(serverNode);
        return (uid, context.Pair.Client.EntMan.GetComponent<XenoArtifactNodeComponent>(uid));
    }

    private static RichTextLabel ScannerState(AnalysisConsoleMenu window) =>
        Descendants(window).OfType<RichTextLabel>().Single(control => control.Name == "ScannerState");

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (var child in parent.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static async Task Scan(TestPair pair, EntityUid serverArtifact)
    {
        var client = pair.Client;
        // Открытие BUI сканера предсказывается клиентом, поэтому нужен настоящий путь ввода на обеих сторонах.
        foreach (var state in new[] { BoundKeyState.Down, BoundKeyState.Up })
        {
            await client.WaitPost(() =>
            {
                var artifact = pair.ToClientUid(serverArtifact);
                var functionId = client.ResolveDependency<IInputManager>().NetworkBindMap.KeyFunctionID(EngineKeyFunctions.Use);
                var message = new ClientFullInputCmdMessage(client.Timing.CurTick, client.Timing.TickFraction, functionId)
                {
                    State = state,
                    Coordinates = client.EntMan.GetComponent<TransformComponent>(artifact).Coordinates,
                    Uid = artifact,
                };
                client.System<InputSystem>().HandleInputCommand(client.Session, EngineKeyFunctions.Use, message);
            });
            await pair.RunTicksSync(1);
        }
        await pair.RunTicksSync(15);
    }

    private static async Task WithScanner(Func<ScannerContext, Task> scenario)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var session = pair.Player!;
        var previousEntity = session.AttachedEntity;
        EntityUid user = default;
        EntityUid scanner = default;
        EntityUid artifact = default;
        Entity<XenoArtifactNodeComponent>? root = null;
        Entity<XenoArtifactNodeComponent>? child = null;
        var pickedUp = false;
        AnalysisConsoleMenu window = null!;
        try
        {
            await server.WaitPost(() =>
            {
                user = entMan.SpawnEntity("MobHuman", map.GridCoords);
                scanner = entMan.SpawnEntity("NodeScanner", map.GridCoords);
                artifact = entMan.SpawnEntity("DummyArtifactStructure", map.GridCoords);
                var artifacts = server.System<SharedXenoArtifactSystem>();
                artifacts.AddNode((artifact, null), "FishNodeScannerTestNode", out root);
                artifacts.AddNode((artifact, null), "FishNodeScannerTestNode", out child);
                artifacts.AddEdge((artifact, null), root!.Value, child!.Value);
                server.PlayerMan.SetAttachedEntity(session, user);
                pickedUp = server.System<SharedHandsSystem>().TryPickupAnyHand(user, scanner);
            });
            await pair.RunTicksSync(15);
            await server.WaitAssertion(() => Assert.That(pickedUp, Is.True));
            await Scan(pair, artifact);
            await server.WaitAssertion(() =>
            {
                Assert.That(entMan.GetComponent<NodeScannerConnectedComponent>(scanner).AttachedTo, Is.EqualTo(artifact));
                Assert.That(entMan.GetComponent<NodeScannerComponent>(scanner).MaxLinkedRange, Is.EqualTo(5));
                Assert.That(server.System<SharedUserInterfaceSystem>().IsUiOpen(scanner, NodeScannerUiKey.Key, user), Is.True);
            });
            await client.WaitAssertion(() => Assert.That(client.System<UserInterfaceSystem>()
                .TryGetOpenUi<NodeScannerBoundUserInterface>(pair.ToClientUid(scanner), NodeScannerUiKey.Key, out _), Is.True));
            await client.WaitPost(() => window = client.ResolveDependency<IUserInterfaceManager>()
                .WindowRoot.Children.OfType<AnalysisConsoleMenu>().Single());
            await scenario(new ScannerContext(pair, user, scanner, artifact, root!.Value, child!.Value, window));
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.System<SharedUserInterfaceSystem>().CloseUi(scanner, NodeScannerUiKey.Key);
                server.PlayerMan.SetAttachedEntity(session, previousEntity);
                entMan.DeleteEntity(map.MapUid);
            });
            await pair.RunTicksSync(5);
        }
        await pair.CleanReturnAsync();
    }

    private sealed record ScannerContext(TestPair Pair, EntityUid User, EntityUid Scanner, EntityUid Artifact,
        EntityUid RootNode, EntityUid ChildNode, AnalysisConsoleMenu Window);
}
