using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client._Fish.Medical.Surgery;
using Content.Client._Starlight.Medical.Surgery;
using Content.Client.Body;
using Content.Server._Sunrise.Misc.ShiftedAsciiTableAccent;
using Content.Shared._Sunrise.MarkingEffects;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Starlight.Medical.Surgery;
using Content.Shared.Starlight.Medical.Surgery.Events;
using Moq;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Fish.Medical.Surgery;

[TestFixture]
public sealed class FishSurgeryRegressionTest
{
    /// <summary>Hair tint and gradient shaders are preserved without leaking shader state to other controls.</summary>
    [TestCase(false)]
    [TestCase(true)]
    public async Task DiagramDrawsHairColorAndShader(bool gradient)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        EntityUid patient = default;
        FishSurgeryBodyDiagram diagram = null;
        Texture hair = null;
        ShaderInstance hairShader = null;
        var draws = new List<(Texture Texture, Color Color, ShaderInstance Shader)>();
        var color = Color.FromHex("#B13C91");
        ShaderInstance finalShader = null;
        await client.WaitPost(() =>
        {
            patient = client.EntMan.SpawnEntity("AppearanceHuman", MapCoordinates.Nullspace);
            var marking = new Marking("HumanHairShorthair2", new[] { color });
            if (gradient)
                marking.SetMarkingEffect(new GradientMarkingEffect(color));
            client.System<VisualBodySystem>().ApplyMarkings(patient, new()
            {
                ["Head"] = new() { [HumanoidVisualLayers.Hair] = new() { marking } },
            });
            var sprites = client.System<SpriteSystem>();
            sprites.TryGetLayer(patient, "HumanHairShorthair2-shorthair2", out var layer, false);
            hair = layer.ActualState.Frame0;
            hairShader = layer.Shader;
            diagram = new FishSurgeryBodyDiagram { Patient = patient };
            diagram.Measure(new Vector2(230, 330));
            diagram.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(230, 330)));

            // Записываем реальные команды рисования схемы, не подменяя получение внешности.
            ShaderInstance currentShader = null;
            var handle = new Mock<DrawingHandleScreen>(hair) { CallBase = true };
            handle.Setup(h => h.UseShader(It.IsAny<ShaderInstance>()))
                .Callback<ShaderInstance>(shader => currentShader = shader);
            handle.Setup(h => h.GetShader()).Returns(() => currentShader);
            handle.Setup(h => h.DrawTextureRectRegion(It.IsAny<Texture>(), It.IsAny<UIBox2>(),
                    It.IsAny<UIBox2?>(), It.IsAny<Color?>()))
                .Callback<Texture, UIBox2, UIBox2?, Color?>((texture, _, _, tint) =>
                    draws.Add((texture, tint ?? Color.White, currentShader)));
            typeof(FishSurgeryBodyDiagram).GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!
                .Invoke(diagram, new object[] { handle.Object });
            finalShader = currentShader;
        });
        await client.WaitAssertion(() =>
        {
            var draw = draws.Single(d => d.Texture == hair);
            Assert.That(draw.Color, Is.EqualTo(gradient ? Color.White : color));
            if (gradient)
            {
                Assert.That(hairShader, Is.Not.Null);
                Assert.That(draw.Shader, Is.SameAs(hairShader), "Gradient hair must retain its coloring shader.");
            }
            Assert.That(finalShader, Is.Null, "Hair coloring must not leak into the rest of the interface.");
        });
        await client.WaitPost(() =>
        {
            diagram.Dispose();
            client.EntMan.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    /// <summary>An open surgery BUI refreshes amputated anatomy without another surgery state snapshot.</summary>
    [TestCase("LegLeft", HumanoidVisualLayers.LLeg)]
    [TestCase("LegRight", HumanoidVisualLayers.RLeg)]
    public async Task OpenBuiRemovesAmputatedLegWithoutNewSnapshot(string category, HumanoidVisualLayers layerKey)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var session = server.PlayerMan.GetSessionById(client.Session!.UserId);
        var previousEntity = session.AttachedEntity;
        EntityUid patient = default;
        EntityUid leg = default;
        NetEntity netPatient = default;
        NetEntity netLeg = default;
        SurgeryBui bui = null;
        SurgeryBuiState initialState = null;
        FishSurgeryWindow window = null;
        FishSurgeryBodyDiagram diagram = null;
        Texture legTexture = null;
        await server.WaitPost(() =>
        {
            patient = server.EntMan.SpawnEntity("AppearanceHuman", map.GridCoords);
            server.EntMan.EnsureComponent<SurgeryTargetComponent>(patient);
            server.EntMan.EnsureComponent<EyeComponent>(patient);
            var body = server.EntMan.GetComponent<BodyComponent>(patient);
            leg = body.Organs!.ContainedEntities.Single(uid =>
                server.EntMan.GetComponent<OrganComponent>(uid).Category?.Id == category);
            netPatient = server.EntMan.GetNetEntity(patient);
            netLeg = server.EntMan.GetNetEntity(leg);
            server.PlayerMan.SetAttachedEntity(session, patient);
            var ui = server.System<SharedUserInterfaceSystem>();
            ui.SetUiState(patient, SurgeryUIKey.Key, new SurgeryBuiState
            {
                Choices = new() { [netLeg] = new() },
            });
            ui.OpenUi(patient, SurgeryUIKey.Key, patient);
        });
        // Открытие BUI и органы должны дойти до клиента до сохранения снимка диаграммы.
        await pair.RunTicksSync(15);
        await client.WaitAssertion(() =>
        {
            var clientPatient = client.EntMan.GetEntity(netPatient);
            var ui = client.System<UserInterfaceSystem>();
            Assert.That(ui.TryGetOpenUi(clientPatient, SurgeryUIKey.Key, out bui), Is.True);
            Assert.That(ui.TryGetUiState(clientPatient, SurgeryUIKey.Key, out initialState), Is.True);
            window = (FishSurgeryWindow) typeof(SurgeryBui)
                .GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bui)!;
            Assert.That(window.IsOpen, Is.True);
            Assert.That(client.System<SpriteSystem>()
                .TryGetLayer(clientPatient, layerKey, out var layer, false), Is.True);
            legTexture = layer.ActualState.Frame0;
            diagram = window.FindControl<FishSurgeryBodyDiagram>("BodyDiagram");
            Assert.That(diagram.AppearanceLayers.Any(l => l.Texture == legTexture), Is.True);
        });

        // Новый снимок операций не отправляем: обновление должно пройти через событие удаления органа.
        await server.WaitPost(() => server.EntMan.DeleteEntity(leg));
        await pair.RunTicksSync(15);
        await client.WaitAssertion(() =>
        {
            Assert.That(client.EntMan.TryGetEntity(netLeg, out var clientLeg) &&
                client.EntMan.EntityExists(clientLeg), Is.False);
            Assert.That(window.IsOpen, Is.True);
            Assert.That(client.System<UserInterfaceSystem>().TryGetUiState(
                client.EntMan.GetEntity(netPatient), SurgeryUIKey.Key, out SurgeryBuiState state), Is.True);
            Assert.That(state, Is.SameAs(initialState), "Anatomy refresh must not depend on a new BUI snapshot.");
            Assert.That(diagram.AppearanceLayers, Is.Not.Empty);
            Assert.That(diagram.AppearanceLayers.Any(l => l.Texture == legTexture), Is.False);
        });

        await server.WaitPost(() =>
        {
            server.System<SharedUserInterfaceSystem>().CloseUi(patient, SurgeryUIKey.Key, patient);
            server.PlayerMan.SetAttachedEntity(session, previousEntity);
            server.EntMan.DeleteEntity(patient);
        });
        await pair.RunTicksSync(5);
        await pair.CleanReturnAsync();
    }

    /// <summary>Server-only accents allow client-side surgery selection and are removed by the server on completion.</summary>
    [Test]
    public async Task ArtifactAccentCanBeTreatedWithoutClientAccentComponent()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        EntityUid patient = default;
        EntityUid operation = default;
        EntityUid step = default;
        EntityUid clientPatient = default;
        EntityUid clientOperation = default;
        SurgeryValidEvent serverValid = default;
        SurgeryValidEvent clientValid = default;
        await server.WaitPost(() =>
        {
            patient = server.EntMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            server.EntMan.AddComponent<AnomalyAccentComponent>(patient);
            operation = server.EntMan.SpawnEntity("SurgeryEliminateVocalCordDefects", MapCoordinates.Nullspace);
            step = server.EntMan.SpawnEntity("SurgeryStepSutureIncision", MapCoordinates.Nullspace);
            var head = server.EntMan.GetComponent<BodyComponent>(patient).Organs!.ContainedEntities.Single(uid =>
                server.EntMan.GetComponent<OrganComponent>(uid).Category?.Id == "Head");
            serverValid = new SurgeryValidEvent(patient, head);
            server.EntMan.EventBus.RaiseLocalEvent(operation, ref serverValid);
        });
        await server.WaitAssertion(() => Assert.That(serverValid.Cancelled, Is.False));
        await client.WaitPost(() =>
        {
            clientOperation = client.EntMan.SpawnEntity("SurgeryEliminateVocalCordDefects", MapCoordinates.Nullspace);
            clientPatient = client.EntMan.SpawnEntity("AppearanceHuman", MapCoordinates.Nullspace);
            var head = client.EntMan.GetComponent<BodyComponent>(clientPatient).Organs!.ContainedEntities.Single(uid =>
                client.EntMan.GetComponent<OrganComponent>(uid).Category?.Id == "Head");
            clientValid = new SurgeryValidEvent(clientPatient, head);
            client.EntMan.EventBus.RaiseLocalEvent(clientOperation, ref clientValid);
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(clientValid.Cancelled, Is.False, "A server-only accent must not block the client's surgery request.");
        });
        await server.WaitPost(() =>
        {
            var complete = new SurgeryStepEvent(patient, patient, patient, new())
            {
                SurgeryProto = "SurgeryEliminateVocalCordDefects", StepProto = "SurgeryStepSutureIncision",
            };
            server.EntMan.EventBus.RaiseLocalEvent(step, ref complete);
        });
        await pair.RunTicksSync(1);
        await server.WaitAssertion(() => Assert.That(server.EntMan.HasComponent<AnomalyAccentComponent>(patient), Is.False));
        await client.WaitPost(() =>
        {
            client.EntMan.DeleteEntity(clientOperation);
            client.EntMan.DeleteEntity(clientPatient);
        });
        await server.WaitPost(() =>
        {
            server.EntMan.DeleteEntity(patient);
            server.EntMan.DeleteEntity(operation);
            server.EntMan.DeleteEntity(step);
        });
        await pair.CleanReturnAsync();
    }
}
