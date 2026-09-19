using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client._Fish.Medical.Surgery;
using Content.Client._Starlight.Medical.Surgery;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Starlight.Medical.Surgery;
using Content.Shared.Starlight.Medical.Surgery.Effects.Step;
using Content.Shared.Starlight.Medical.Surgery.Steps.Parts;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.Medical.Surgery;

[TestFixture]
public sealed class FishSurgeryWindowTest
{
    /// <summary>Исходный путь обновления Sunrise остаётся совместимым с Fish-окном.</summary>
    [Test]
    public async Task UpstreamUpdatePathRemainsCompatible()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        SurgeryBui bui = null;
        FishSurgeryWindow window = null;
        EntityUid patient = default;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;

        await client.WaitPost(() =>
        {
            patient = client.EntMan.SpawnEntity("AppearanceHuman", MapCoordinates.Nullspace);
            var body = client.EntMan.GetComponent<BodyComponent>(patient);
            var hand = body.Organs!.ContainedEntities.First(uid =>
                client.EntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "OrganHumanHandLeft");
            var state = new SurgeryBuiState
            {
                Choices = new() { [client.EntMan.GetNetEntity(hand)] = new() },
            };

            bui = new SurgeryBui(patient, SurgeryUIKey.Key);
            var update = typeof(SurgeryBui).GetMethod(
                "Update",
                flags,
                null,
                new[] { typeof(SurgeryBuiState) },
                null)!;
            update.Invoke(bui, new object[] { state });
            window = (FishSurgeryWindow) typeof(SurgeryBui).GetField("_window", flags)!.GetValue(bui)!;
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(window.Parts.Children.Count(), Is.EqualTo(1));
            Assert.That(window.Parts.Parent?.Visible, Is.False,
                "The compatibility list must remain outside the visible Fish interface.");
        });
        await client.WaitPost(() =>
        {
            bui.Dispose();
            client.EntMan.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    /// <summary>Changing available operations after an incision must not navigate away from the active operation.</summary>
    [TestCase(false)]
    [TestCase(true)]
    public async Task IncisionSnapshotPreservesSelectedOperation(bool omitPart)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        SurgeryBui bui = null;
        FishSurgeryWindow window = null;
        EntityUid patient = default;
        EntityUid hand = default;
        EntityUid surgery = default;
        NetEntity netHand = default;
        Control[] rows = null;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var update = typeof(SurgeryBui).GetMethod("UpdateState", flags)!;
        var stateProperty = typeof(BoundUserInterface).GetProperty("State", flags)!;
        SurgeryBuiState state = null;

        await client.WaitPost(() =>
        {
            patient = client.EntMan.SpawnEntity("AppearanceHuman", MapCoordinates.Nullspace);
            var body = client.EntMan.GetComponent<BodyComponent>(patient);
            hand = body.Organs!.ContainedEntities.First(uid =>
                client.EntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "OrganHumanHandLeft");
            surgery = client.EntMan.SpawnEntity("SurgeryOpenIncision", MapCoordinates.Nullspace);
            netHand = client.EntMan.GetNetEntity(hand);
            state = new SurgeryBuiState
            {
                Choices = new() { [netHand] = new() { ("SurgeryOpenIncision", "", false) } },
            };
            bui = new SurgeryBui(patient, SurgeryUIKey.Key);
            stateProperty.SetValue(bui, state);
            update.Invoke(bui, new object[] { state });
            typeof(SurgeryBui).GetMethod("OnPartPressed", flags)!
                .Invoke(bui, new object[] { netHand, state.Choices[netHand] });
            Entity<SurgeryComponent> operation = (surgery, client.EntMan.GetComponent<SurgeryComponent>(surgery));
            typeof(SurgeryBui).GetMethod("OnSurgeryPressed", flags)!
                .Invoke(bui, new object[] { operation, netHand, (EntProtoId) "SurgeryOpenIncision" });
            window = (FishSurgeryWindow) typeof(SurgeryBui).GetField("_window", flags)!.GetValue(bui)!;
            rows = window.Steps.Children.ToArray();

            // Имитируем пришедшее состояние вскрытой области и изменившийся список доступных операций.
            client.EntMan.AddComponent<IncisionOpenComponent>(hand);
            state = new SurgeryBuiState { Choices = new() };
            if (!omitPart)
                state.Choices[netHand] = new() { ("SurgeryCloseIncision", "", false) };
            stateProperty.SetValue(bui, state);
            update.Invoke(bui, new object[] { state });
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(rows, Is.Not.Empty);
            Assert.That(window.Steps.Visible, Is.True);
            Assert.That(window.Steps.Children.ToArray(), Is.EqualTo(rows),
                "A server snapshot must preserve existing step controls.");
            Assert.That(window.FindControl<FishSurgeryBodyDiagram>("BodyDiagram").SelectedPart, Is.EqualTo(hand));
            Assert.That(typeof(SurgeryBui).GetField("_surgery", flags)!.GetValue(bui), Is.Not.Null);
        });
        await client.WaitPost(() =>
        {
            client.EntMan.DeleteEntity(hand);
            update.Invoke(bui, new object[] { state });
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(window.Steps.Visible, Is.False);
            Assert.That(window.FindControl<FishSurgeryBodyDiagram>("BodyDiagram").SelectedPart, Is.Null);
            Assert.That(typeof(SurgeryBui).GetField("_surgery", flags)!.GetValue(bui), Is.Null);
        });
        await client.WaitPost(() =>
        {
            bui.Dispose();
            client.EntMan.DeleteEntity(patient);
            client.EntMan.DeleteEntity(surgery);
        });
        await pair.CleanReturnAsync();
    }

    /// <summary>Step transitions retain the full list and animate only actual status changes.</summary>
    [Test]
    public async Task StepTransitionsPreserveControls()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        FishSurgeryWindow window = null;
        SurgeryStepButton current = null;
        SurgeryStepButton future = null;
        await client.WaitPost(() =>
        {
            window = new FishSurgeryWindow();
            window.OpenCentered();
            window.ShowSelection(null, "Left hand", false, true);
            window.SetOperation("Implant extraction");
            current = new SurgeryStepButton { Step = new EntityUid(41) };
            future = new SurgeryStepButton { Step = new EntityUid(42) };
            window.RegisterStep(current);
            window.RegisterStep(future);
            window.Steps.AddChild(current);
            window.Steps.AddChild(future);
            window.SetStepPresentation(current, true, false);
            window.SetStepPresentation(future, false, false);
            window.Steps.Visible = true;
            window.SetStepPresentation(current, false, true);
            window.SetStepPresentation(future, true, false);
            window.SetStepPresentation(current, false, false);
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(current.Visible, Is.True);
            Assert.That(future.Visible, Is.True);
            Assert.That(window.Steps.Children.ToArray(), Is.EqualTo(new[] { current, future }));
            Assert.That(current.HasRunningAnimation("fish-surgery-fade"), Is.True);
        });
        await pair.RunTicksSync(60);
        await client.WaitPost(() =>
        {
            window.SetStepPresentation(current, false, false);
            window.SetStepPresentation(future, true, false);
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(current.HasRunningAnimation("fish-surgery-fade"), Is.False,
                "An unchanged snapshot must not restart the transition.");
            Assert.That(window.Steps.Children.ToArray(), Is.EqualTo(new[] { current, future }));
        });
        await client.WaitPost(() => window.Dispose());
        await pair.CleanReturnAsync();
    }

    /// <summary>Прогресс остаётся внутри этапа и обновляется без повторного измерения подписи.</summary>
    [Test, Repeat(3)]
    public async Task ActionProgressStaysInsideStep()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        FishSurgeryWindow window = null;
        SurgeryStepButton step = null;
        ProgressBar progress = null;
        var layoutPreserved = false;
        await client.WaitPost(() =>
        {
            window = new FishSurgeryWindow();
            window.OpenCentered();
            step = new SurgeryStepButton { Step = new EntityUid(43) };
            window.RegisterStep(step);
            window.Steps.AddChild(step);
            window.SetStepPresentation(step, true, false);
            progress = step.Button.Children.OfType<ProgressBar>().Single();
            step.NameLabel.Measure(new Vector2(400, 40));
            window.SetActionProgress(step.Step, "Cutting", 0f);
            for (var i = 0; i <= 100; i++)
                window.SetActionFraction(step.Step, i / 100f);
            layoutPreserved = step.NameLabel.IsMeasureValid;
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(layoutPreserved, Is.True, "Progress must not invalidate the step label's layout.");
            Assert.That(progress.Parent, Is.EqualTo(step.Button));
            Assert.That(progress.Visible, Is.True);
            Assert.That(progress.Value, Is.EqualTo(1f));
            Assert.That(progress.HasStyleClass("FishSurgeryProgressDanger"), Is.False);
        });
        await client.WaitPost(() => window.SetActionProgress(step.Step, "Interrupted", 0.4f, true));
        await client.WaitAssertion(() =>
        {
            Assert.That(progress.ToolTip, Is.EqualTo("Interrupted"));
            Assert.That(progress.Value, Is.EqualTo(0.4f));
            Assert.That(progress.HasStyleClass("FishSurgeryProgressDanger"), Is.True);
        });
        await client.WaitPost(window.ClearActionProgress);
        await client.WaitAssertion(() => Assert.That(progress.Visible, Is.False));
        await client.WaitPost(() => window.Dispose());
        await pair.CleanReturnAsync();
    }

    /// <summary>Unchanged snapshots retain selection, confirmation and text layout; removed anatomy is cleared.</summary>
    [Test, Repeat(3)]
    public async Task SnapshotRefreshPreservesInteraction()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        FishSurgeryWindow window = null;
        FishSurgeryBodyDiagram diagram = null;
        EntityUid hand = default;
        Entity<OrganComponent> part = default;
        var layoutPreserved = false;

        await client.WaitPost(() =>
        {
            hand = client.EntMan.SpawnEntity("OrganHumanHandLeft", MapCoordinates.Nullspace);
            part = (hand, client.EntMan.GetComponent<OrganComponent>(hand));
            window = new FishSurgeryWindow();
            window.SetParts(new[] { (part, "Test hand") });
            var step = new SurgeryStepButton { Step = new EntityUid(44) };
            window.RegisterStep(step);
            window.Steps.AddChild(step);
            window.SetStepPresentation(step, true, false);
            window.ShowSelection(hand, "Test hand", false, true);
            window.SetAreaStatus(false, false);
            window.SetOperation(null);
            window.RequestConfirmation(step.Step, "Dangerous step", () => { });
            window.OpenCentered();
            diagram = window.FindControl<FishSurgeryBodyDiagram>("BodyDiagram");

            var stateLabel = window.FindControl<RichTextLabel>("SelectedPart");
            stateLabel.Measure(new Vector2(400, 80));
            window.SetParts(new[] { (part, "Test hand") });
            window.ShowSelection(hand, "Test hand", false, true);
            window.SetOperation(null);
            layoutPreserved = stateLabel.IsMeasureValid;
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(diagram.SelectedPart, Is.EqualTo(hand));
            Assert.That(window.FindControl<PanelContainer>("ConfirmationPanel").Visible, Is.True);
            Assert.That(window.FindControl<PanelContainer>("ConfirmationPanel").Parent, Is.EqualTo(window.Steps));
            Assert.That(layoutPreserved, Is.True, "Identical text must not invalidate layout.");
        });

        await client.WaitPost(() => window.SetOperation("Operation"));
        await client.WaitAssertion(() =>
            Assert.That(window.FindControl<PanelContainer>("ConfirmationPanel").Visible, Is.False,
                "A different surgical step must invalidate the old confirmation."));

        // Изменение подписи обновляет снимок, но не сбрасывает существующую выбранную конечность.
        await client.WaitPost(() => window.SetParts(new[] { (part, "Renamed hand") }));
        await client.WaitAssertion(() => Assert.That(diagram.SelectedPart, Is.EqualTo(hand)));
        await client.WaitPost(() =>
        {
            window.SetParts(Array.Empty<(Entity<OrganComponent>, string)>());
            window.ShowSelection(null, null, true, false);
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(diagram.SelectedPart, Is.Null);
            Assert.That(window.FindControl<PanelContainer>("ConfirmationPanel").Visible, Is.False);
        });
        await client.WaitPost(() =>
        {
            window.Dispose();
            client.EntMan.DeleteEntity(hand);
        });
        await pair.CleanReturnAsync();
    }

    /// <summary>Авторитетное состояние возвращает завершённый этап после серверного сброса.</summary>
    [Test]
    public async Task AuthoritativeRollbackRestoresCurrentStep()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        SurgeryBui bui = null;
        FishSurgeryWindow window = null;
        EntityUid patient = default;
        EntityUid hand = default;
        EntityUid surgery = default;
        SurgeryProgressComponent progress = null;
        HashSet<EntProtoId> completedSteps = null;
        SurgeryStepButton firstStep = null;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var update = typeof(SurgeryBui).GetMethod("UpdateState", flags)!;
        var stateProperty = typeof(BoundUserInterface).GetProperty("State", flags)!;

        await client.WaitPost(() =>
        {
            patient = client.EntMan.SpawnEntity("AppearanceHuman", MapCoordinates.Nullspace);
            var body = client.EntMan.GetComponent<BodyComponent>(patient);
            hand = body.Organs!.ContainedEntities.First(uid =>
                client.EntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "OrganHumanHandLeft");
            surgery = client.EntMan.SpawnEntity("SurgeryOpenIncision", MapCoordinates.Nullspace);
            progress = client.EntMan.EnsureComponent<SurgeryProgressComponent>(hand);
            completedSteps = (HashSet<EntProtoId>) typeof(SurgeryProgressComponent)
                .GetField("CompletedSteps")!.GetValue(progress)!;
            var netHand = client.EntMan.GetNetEntity(hand);
            var state = new SurgeryBuiState
            {
                Choices = new() { [netHand] = new() { ("SurgeryOpenIncision", "", false) } },
            };
            bui = new SurgeryBui(patient, SurgeryUIKey.Key);
            stateProperty.SetValue(bui, state);
            update.Invoke(bui, new object[] { state });
            typeof(SurgeryBui).GetMethod("OnPartPressed", flags)!
                .Invoke(bui, new object[] { netHand, state.Choices[netHand] });
            Entity<SurgeryComponent> operation = (surgery, client.EntMan.GetComponent<SurgeryComponent>(surgery));
            typeof(SurgeryBui).GetMethod("OnSurgeryPressed", flags)!
                .Invoke(bui, new object[] { operation, netHand, (EntProtoId) "SurgeryOpenIncision" });
            window = (FishSurgeryWindow) typeof(SurgeryBui).GetField("_window", flags)!.GetValue(bui)!;
            firstStep = window.Steps.Children.OfType<SurgeryStepButton>().First();

            completedSteps.Add($"SurgeryOpenIncision:{client.EntMan.GetComponent<MetaDataComponent>(firstStep.Step).EntityPrototype!.ID}");
            bui.Update();
        });
        await client.WaitAssertion(() =>
            Assert.That(firstStep.Button.HasStyleClass("FishSurgeryDone"), Is.True));

        await client.WaitPost(() =>
        {
            completedSteps.Clear();
            bui.Update();
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(firstStep.Button.HasStyleClass("FishSurgeryDone"), Is.False);
            Assert.That(firstStep.Button.HasStyleClass("FishSurgeryNext"), Is.True);
        });

        await client.WaitPost(() =>
        {
            bui.Dispose();
            client.EntMan.DeleteEntity(patient);
            client.EntMan.DeleteEntity(surgery);
        });
        await pair.CleanReturnAsync();
    }

    /// <summary>Удаление выбранной операции сразу инвалидирует подтверждение опасного этапа.</summary>
    [Test]
    public async Task RemovedOperationInvalidatesConfirmation()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        SurgeryBui bui = null;
        FishSurgeryWindow window = null;
        EntityUid patient = default;
        EntityUid surgery = default;
        var confirmations = 0;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var update = typeof(SurgeryBui).GetMethod("UpdateState", flags)!;
        var stateProperty = typeof(BoundUserInterface).GetProperty("State", flags)!;

        await client.WaitPost(() =>
        {
            patient = client.EntMan.SpawnEntity("AppearanceHuman", MapCoordinates.Nullspace);
            var body = client.EntMan.GetComponent<BodyComponent>(patient);
            var hand = body.Organs!.ContainedEntities.First(uid =>
                client.EntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "OrganHumanHandLeft");
            surgery = client.EntMan.SpawnEntity("SurgeryOpenIncision", MapCoordinates.Nullspace);
            var netHand = client.EntMan.GetNetEntity(hand);
            var state = new SurgeryBuiState
            {
                Choices = new() { [netHand] = new() { ("SurgeryOpenIncision", "", false) } },
            };
            bui = new SurgeryBui(patient, SurgeryUIKey.Key);
            stateProperty.SetValue(bui, state);
            update.Invoke(bui, new object[] { state });
            typeof(SurgeryBui).GetMethod("OnPartPressed", flags)!
                .Invoke(bui, new object[] { netHand, state.Choices[netHand] });
            Entity<SurgeryComponent> operation = (surgery, client.EntMan.GetComponent<SurgeryComponent>(surgery));
            typeof(SurgeryBui).GetMethod("OnSurgeryPressed", flags)!
                .Invoke(bui, new object[] { operation, netHand, (EntProtoId) "SurgeryOpenIncision" });
            window = (FishSurgeryWindow) typeof(SurgeryBui).GetField("_window", flags)!.GetValue(bui)!;
            var step = window.Steps.Children.OfType<SurgeryStepButton>().First();
            window.RequestConfirmation(step.Step, "Dangerous step", () => confirmations++);

            bui.InvalidateFishSurgery(surgery);
            window.ConfirmPendingAction();
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(window.FindControl<PanelContainer>("ConfirmationPanel").Visible, Is.False);
            Assert.That(confirmations, Is.Zero);
            Assert.That(typeof(SurgeryBui).GetField("_surgery", flags)!.GetValue(bui), Is.Null);
        });

        await client.WaitPost(() =>
        {
            bui.Dispose();
            client.EntMan.DeleteEntity(patient);
            client.EntMan.DeleteEntity(surgery);
        });
        await pair.CleanReturnAsync();
    }

    /// <summary>Повторное открытие BUI не использует прогресс и подтверждение прошлой локальной сессии.</summary>
    [Test]
    public async Task ReopenClearsTransientActionState()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        SurgeryBui bui = null;
        FishSurgeryWindow window = null;
        EntityUid patient = default;
        EntityUid surgery = default;
        ProgressBar progress = null;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var update = typeof(SurgeryBui).GetMethod("UpdateState", flags)!;
        var open = typeof(SurgeryBui).GetMethod("Open", flags)!;
        var stateProperty = typeof(BoundUserInterface).GetProperty("State", flags)!;

        await client.WaitPost(() =>
        {
            patient = client.EntMan.SpawnEntity("AppearanceHuman", MapCoordinates.Nullspace);
            var body = client.EntMan.GetComponent<BodyComponent>(patient);
            var hand = body.Organs!.ContainedEntities.First(uid =>
                client.EntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "OrganHumanHandLeft");
            surgery = client.EntMan.SpawnEntity("SurgeryOpenIncision", MapCoordinates.Nullspace);
            var netHand = client.EntMan.GetNetEntity(hand);
            var state = new SurgeryBuiState
            {
                Choices = new() { [netHand] = new() { ("SurgeryOpenIncision", "", false) } },
            };
            bui = new SurgeryBui(patient, SurgeryUIKey.Key);
            stateProperty.SetValue(bui, state);
            open.Invoke(bui, null);
            typeof(SurgeryBui).GetMethod("OnPartPressed", flags)!
                .Invoke(bui, new object[] { netHand, state.Choices[netHand] });
            Entity<SurgeryComponent> operation = (surgery, client.EntMan.GetComponent<SurgeryComponent>(surgery));
            typeof(SurgeryBui).GetMethod("OnSurgeryPressed", flags)!
                .Invoke(bui, new object[] { operation, netHand, (EntProtoId) "SurgeryOpenIncision" });
            window = (FishSurgeryWindow) typeof(SurgeryBui).GetField("_window", flags)!.GetValue(bui)!;
            var step = window.Steps.Children.OfType<SurgeryStepButton>().First();
            progress = step.Button.Children.OfType<ProgressBar>().Single();
            window.SetActionProgress(step.Step, "Old action", 0.75f);
            window.RequestConfirmation(step.Step, "Dangerous step", () => { });
            typeof(SurgeryBui).GetField("_fishRequestUntil", flags)!
                .SetValue(bui, TimeSpan.FromHours(1));
            typeof(SurgeryBui).GetField("_fishProgressKey", flags)!
                .SetValue(bui, ((ushort) 7, false, false));

            bui.Close();
            open.Invoke(bui, null);
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(progress.Visible, Is.False);
            Assert.That(window.FindControl<PanelContainer>("ConfirmationPanel").Visible, Is.False);
            Assert.That(typeof(SurgeryBui).GetField("_fishPreviousActionId", flags)!.GetValue(bui), Is.Null);
            Assert.That(typeof(SurgeryBui).GetField("_fishProgressKey", flags)!.GetValue(bui), Is.Null);
        });

        await client.WaitPost(() =>
        {
            bui.Dispose();
            client.EntMan.DeleteEntity(patient);
            client.EntMan.DeleteEntity(surgery);
        });
        await pair.CleanReturnAsync();
    }

    /// <summary>Uses resolved species layers, preserves eyes and omits unmapped equipment layers.</summary>
    [TestCase("AppearanceHuman")]
    [TestCase("AppearanceReptilian")]
    [TestCase("AppearanceVox")]
    [TestCase("AppearanceMoth")]
    [TestCase("AppearanceFelinid")]
    public async Task DiagramUsesPatientAppearance(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        EntityUid patient = default;
        FishSurgeryBodyDiagram diagram = null;
        Texture head = null;
        Texture eyes = null;
        var layerCount = 0;
        await client.WaitPost(() =>
        {
            patient = client.EntMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
            var sprites = client.System<SpriteSystem>();
            sprites.LayerMapTryGet(patient, HumanoidVisualLayers.Head, out var headIndex, false);
            sprites.LayerMapTryGet(patient, HumanoidVisualLayers.Eyes, out var eyesIndex, false);
            if (sprites.TryGetLayer(patient, headIndex, out var headLayer, false))
                head = headLayer.ActualState?.Frame0;
            if (sprites.TryGetLayer(patient, eyesIndex, out var eyeLayer, false))
                eyes = eyeLayer.ActualState?.Frame0;
            diagram = new FishSurgeryBodyDiagram { Patient = patient };
            diagram.RefreshAppearance();
            layerCount = diagram.AppearanceLayers.Count;
            // Одежда без анатомического ключа не должна появляться на схеме.
            sprites.AddTextureLayer(patient, head);
            diagram.RefreshAppearance();
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(head, Is.Not.Null);
            Assert.That(eyes, Is.Not.Null);
            Assert.That(diagram.AppearanceLayers.Any(layer => layer.Texture == head), Is.True);
            Assert.That(diagram.AppearanceLayers.Any(layer => layer.Texture == eyes), Is.True);
            Assert.That(diagram.AppearanceLayers.Count, Is.EqualTo(layerCount));
        });
        await client.WaitPost(() =>
        {
            diagram.Dispose();
            client.EntMan.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Exercises the actual window and UI input, including stale clicks after a state refresh.
    /// </summary>
    [TestCase("OrganHumanHandLeft", 23, 20)]
    [TestCase("OrganHumanHandRight", 8, 20)]
    [TestCase("OrganHumanFootLeft", 18, 30)]
    public async Task PartSelectionClearsWithSnapshot(string prototype, float x, float y)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        FishSurgeryWindow window = null;
        FishSurgeryBodyDiagram diagram = null;
        EntityUid hand = default;
        EntityUid? selected = null;

        await client.WaitPost(() =>
        {
            hand = client.EntMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
            window = new FishSurgeryWindow();
            window.SetPatient("Test patient");
            window.AddPart((hand, client.EntMan.GetComponent<OrganComponent>(hand)), "Test part");
            var step = new SurgeryStepButton { Step = new EntityUid(45) };
            window.RegisterStep(step);
            window.Steps.AddChild(step);
            window.SetStepPresentation(step, true, false);
            window.PartSelected += part => selected = part;
            window.OpenCentered();
            window.Measure(new Vector2(820, 620));
            window.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(820, 620)));
            diagram = window.FindControl<FishSurgeryBodyDiagram>("BodyDiagram");
        });

        await client.WaitAssertion(() =>
        {
            Assert.That(window.FindControl<Robust.Client.UserInterface.Controls.OptionButton>("AdditionalParts").Visible, Is.False);
            Assert.That(diagram.PixelWidth, Is.GreaterThan(0));
        });

        // Проверяем зеркальное расположение сторон пациента и нажатия на маленькие конечности.
        var canvas = FishSurgeryBodyDiagram.GetCanvas(diagram.PixelSize);
        var pixel = canvas.TopLeft + new Vector2(x, y) * (canvas.Width / 32f);
        await Click(pixel);
        await client.WaitAssertion(() => Assert.That(selected, Is.EqualTo(hand)));

        await client.WaitPost(() =>
        {
            window.ShowSelection(hand, "Test part", false, false);
            window.ClearParts();
            selected = null;
        });
        await Click(pixel);
        await client.WaitAssertion(() =>
        {
            Assert.That(selected, Is.Null, "A removed part must no longer be clickable.");
            Assert.That(diagram.SelectedPart, Is.Null);
        });

        // Подтверждение нельзя повторно использовать или перенести на другую область.
        var confirmations = 0;
        await client.WaitPost(() => window.RequestConfirmation(new EntityUid(45), "Dangerous step", () => confirmations++));
        await client.WaitAssertion(() => Assert.That(confirmations, Is.Zero));
        await client.WaitPost(() =>
        {
            window.ConfirmPendingAction();
            window.ConfirmPendingAction();
        });
        await client.WaitAssertion(() => Assert.That(confirmations, Is.EqualTo(1)));
        await client.WaitPost(() =>
        {
            window.RequestConfirmation(new EntityUid(45), "Dangerous step", () => confirmations++);
            window.ShowSelection(null, null, true, false);
            window.ConfirmPendingAction();
        });
        await client.WaitAssertion(() => Assert.That(confirmations, Is.EqualTo(1)));

        await client.WaitPost(() =>
        {
            window.Dispose();
            client.EntMan.DeleteEntity(hand);
        });
        await pair.CleanReturnAsync();

        async Task Click(Vector2 relativePixel)
        {
            foreach (var state in new[] { BoundKeyState.Down, BoundKeyState.Up })
            {
                await client.DoGuiEvent(diagram, new GUIBoundKeyEventArgs(
                    EngineKeyFunctions.UIClick, state,
                    new ScreenCoordinates(diagram.GlobalPixelPosition + relativePixel, default), false,
                    relativePixel / diagram.UIScale, relativePixel));
            }
        }
    }
}
