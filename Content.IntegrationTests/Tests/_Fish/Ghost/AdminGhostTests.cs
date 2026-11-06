#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Administration.Managers;
using Content.Shared._Fish.Ghost;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Server.Console;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Fish.Ghost;

[TestFixture]
public sealed class AdminGhostTests : GameTest
{
    /// <summary>
    /// Должность администратора, которой подписывается имя агоста в тесте.
    /// </summary>
    private const string AdminTitle = "Тестовый админ";

    /// <summary>
    /// Самозапрет на взаимодействие должен быть включён сразу при входе в агоста,
    /// переключение кнопки — без задержки, но с кулдауном на кнопке.
    /// </summary>
    [Test]
    public async Task AghostInteractionRestrictedByDefault()
    {
        var pair = Pair;
        var server = pair.Server;

        var entMan = server.ResolveDependency<IServerEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var serverConsole = server.ResolveDependency<IServerConsoleHost>();
        var adminMan = server.ResolveDependency<IAdminManager>();

        var player = playerMan.Sessions.Single();
        var testMap = await pair.CreateTestMap();

        // У игрока должно быть сознание и тело, иначе команда aghost не сработает.
        await server.WaitAssertion(() =>
        {
            var mindSystem = entMan.System<SharedMindSystem>();

            var body = entMan.SpawnEntity(null, testMap.GridCoords);
            entMan.EnsureComponent<MindContainerComponent>(body);

            var mind = mindSystem.CreateMind(player.UserId, "Aghost Tester");
            mindSystem.TransferTo(mind, body, mind: mind);
        });

        await pair.RunUntilSynced();

        await server.WaitAssertion(() =>
        {
            // Должность админа должна попасть в имя призрака (AdminGhostTitleSystem).
            var adminData = adminMan.GetAdminData(player);
            Assert.That(adminData, Is.Not.Null);

            var oldTitle = adminData!.Title;
            adminData.Title = AdminTitle;
            serverConsole.ExecuteCommand(player, "aghost");
            adminData.Title = oldTitle;
        });

        await pair.RunUntilSynced();

        EntityUid ghost = default!;
        EntityUid item = default!;

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.Not.Null);
            ghost = player.AttachedEntity!.Value;
            item = entMan.SpawnEntity("ClothingBackpack", MapCoordinates.Nullspace);

            // Имя агоста подписано должностью администратора.
            Assert.That(entMan.GetComponent<MetaDataComponent>(ghost).EntityName, Does.Contain(AdminTitle));

            var comp = entMan.GetComponent<AdminGhostComponent>(ghost);

            // Запрет включён сразу, без дополнительных действий.
            Assert.That(comp.InteractionRestricted, Is.True);

            var actionUid = comp.InteractionToggleActionEntity;
            Assert.That(actionUid, Is.Not.Null);

            var action = entMan.GetComponent<ActionComponent>(actionUid!.Value);

            // Кнопка сразу отображается во включённом состоянии.
            Assert.That(action.Toggled, Is.True);

            // Кнопка должна работать даже при включённом запрете, иначе его нельзя снять.
            Assert.That(action.CheckCanInteract, Is.False);
        });

        await server.WaitAssertion(() =>
        {
            // Общее взаимодействие без цели (например, строительство) при самозапрете должно блокироваться.
            // Кнопки агоста на эту проверку не опираются: у них checkCanInteract: false.
            var noTargetAttempt = new InteractionAttemptEvent(ghost, null);
            entMan.EventBus.RaiseLocalEvent(ghost, ref noTargetAttempt);
            Assert.That(noTargetAttempt.Cancelled, Is.True);

            // Собственные интерфейсы (BUI) тоже должны открываться: цель совпадает с самим агостом.
            var selfAttempt = new InteractionAttemptEvent(ghost, ghost);
            entMan.EventBus.RaiseLocalEvent(ghost, ref selfAttempt);
            Assert.That(selfAttempt.Cancelled, Is.False);

            // А вот взаимодействие с объектами мира — должно блокироваться.
            var worldAttempt = new InteractionAttemptEvent(ghost, item);
            entMan.EventBus.RaiseLocalEvent(ghost, ref worldAttempt);
            Assert.That(worldAttempt.Cancelled, Is.True);

            // Остальные попытки взаимодействия тоже должны отменяться при включённом запрете.
            var useAttempt = new UseAttemptEvent(ghost, item);
            entMan.EventBus.RaiseLocalEvent(ghost, useAttempt);
            Assert.That(useAttempt.Cancelled, Is.True);

            var pickupAttempt = new PickupAttemptEvent(ghost, item, true);
            entMan.EventBus.RaiseLocalEvent(ghost, pickupAttempt);
            Assert.That(pickupAttempt.Cancelled, Is.True);

            var dropAttempt = new DropAttemptEvent();
            entMan.EventBus.RaiseLocalEvent(ghost, dropAttempt);
            Assert.That(dropAttempt.Cancelled, Is.True);
        });

        // Нажатие переключает состояние сразу, но кнопка уходит в кулдаун.
        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<AdminGhostComponent>(ghost);
            var actionUid = comp.InteractionToggleActionEntity!.Value;
            var action = new Entity<ActionComponent>(actionUid, entMan.GetComponent<ActionComponent>(actionUid));
            var performer = new Entity<ActionsComponent?>(ghost, entMan.GetComponent<ActionsComponent>(ghost));

            entMan.System<SharedActionsSystem>().PerformAction(performer, action);

            // Состояние изменилось сразу, без задержки.
            Assert.That(entMan.GetComponent<AdminGhostComponent>(ghost).InteractionRestricted, Is.False);

            // Кнопка ушла в кулдаун на несколько секунд после нажатия.
            Assert.That(entMan.GetComponent<ActionComponent>(actionUid).Cooldown, Is.Not.Null);
        });

        await server.WaitAssertion(() =>
        {
            // После снятия запрета те же события больше не должны отменяться.
            var interactionAttempt = new InteractionAttemptEvent(ghost, item);
            entMan.EventBus.RaiseLocalEvent(ghost, ref interactionAttempt);
            Assert.That(interactionAttempt.Cancelled, Is.False);

            var noTargetAttempt = new InteractionAttemptEvent(ghost, null);
            entMan.EventBus.RaiseLocalEvent(ghost, ref noTargetAttempt);
            Assert.That(noTargetAttempt.Cancelled, Is.False);

            var useAttempt = new UseAttemptEvent(ghost, item);
            entMan.EventBus.RaiseLocalEvent(ghost, useAttempt);
            Assert.That(useAttempt.Cancelled, Is.False);

            var pickupAttempt = new PickupAttemptEvent(ghost, item, true);
            entMan.EventBus.RaiseLocalEvent(ghost, pickupAttempt);
            Assert.That(pickupAttempt.Cancelled, Is.False);

            var dropAttempt = new DropAttemptEvent();
            entMan.EventBus.RaiseLocalEvent(ghost, dropAttempt);
            Assert.That(dropAttempt.Cancelled, Is.False);
        });
    }

    /// <summary>
    /// Переключение самозапрета с клиента: с включённым предсказанием состояние меняется на клиенте сразу,
    /// с выключенным — только после ответа сервера. В обоих случаях после синхронизации
    /// самозапрет и кнопка совпадают на клиенте и на сервере.
    /// Сетевая задержка в тестах уже включена (см. <c>net.fakelagmin</c> в EntryPoint).
    /// </summary>
    [TestCase(true)]
    [TestCase(false)]
    public async Task AghostToggleFromClient(bool prediction)
    {
        var pair = Pair;
        var server = pair.Server;
        var client = pair.Client;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var cEntMan = client.ResolveDependency<IEntityManager>();
        var cCfg = client.ResolveDependency<IConfigurationManager>();

        var (ghost, action) = await SpawnAghost();

        var netGhost = NetEntity.Invalid;
        await server.WaitPost(() => netGhost = sEntMan.GetNetEntity(ghost));
        await pair.RunUntilSynced();

        await client.WaitPost(() => cCfg.SetCVar(CVars.NetPredict, prediction));
        try
        {
            EntityUid clientGhost = default;
            await client.WaitPost(() => clientGhost = cEntMan.GetEntity(netGhost));

            await client.WaitAssertion(() =>
            {
                var comp = cEntMan.GetComponent<AdminGhostComponent>(clientGhost);
                Assert.That(comp.InteractionRestricted, Is.True);

                cEntMan.RaisePredictiveEvent(new RequestPerformActionEvent(action));

                // С предсказанием состояние меняется сразу, без него — только после ответа сервера.
                Assert.That(comp.InteractionRestricted, Is.EqualTo(!prediction));
            });

            // Сервер событие ещё не обработал: тики на нём не шли.
            await server.WaitAssertion(() =>
            {
                Assert.That(sEntMan.GetComponent<AdminGhostComponent>(ghost).InteractionRestricted, Is.True);
            });

            await pair.RunTicksSync(30);
            await pair.RunUntilSynced();

            // После синхронизации самозапрет снят и на сервере, и на клиенте.
            await server.WaitAssertion(() =>
            {
                var comp = sEntMan.GetComponent<AdminGhostComponent>(ghost);
                Assert.That(comp.InteractionRestricted, Is.False);
                Assert.That(sEntMan.GetComponent<ActionComponent>(sEntMan.GetEntity(action)).Toggled, Is.False);
            });

            await client.WaitAssertion(() =>
            {
                var comp = cEntMan.GetComponent<AdminGhostComponent>(clientGhost);
                Assert.That(comp.InteractionRestricted, Is.False);
                Assert.That(cEntMan.GetComponent<ActionComponent>(cEntMan.GetEntity(action)).Toggled, Is.False);
            });
        }
        finally
        {
            await client.WaitPost(() => cCfg.SetCVar(CVars.NetPredict, true));
        }
    }

    /// <summary>
    /// Создаёт игроку тело и превращает его в админского призрака командой aghost.
    /// </summary>
    /// <returns>Призрак на сервере и сетевой идентификатор кнопки самозапрета.</returns>
    private async Task<(EntityUid Ghost, NetEntity Action)> SpawnAghost()
    {
        var pair = Pair;
        var server = pair.Server;

        var entMan = server.ResolveDependency<IServerEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var serverConsole = server.ResolveDependency<IServerConsoleHost>();

        var player = playerMan.Sessions.Single();
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var mindSystem = entMan.System<SharedMindSystem>();

            var body = entMan.SpawnEntity(null, testMap.GridCoords);
            entMan.EnsureComponent<MindContainerComponent>(body);

            var mind = mindSystem.CreateMind(player.UserId, "Aghost Tester");
            mindSystem.TransferTo(mind, body, mind: mind);
        });

        await pair.RunUntilSynced();

        await server.WaitAssertion(() =>
        {
            serverConsole.ExecuteCommand(player, "aghost");
        });

        await pair.RunUntilSynced();

        EntityUid ghost = default;
        var action = NetEntity.Invalid;

        await server.WaitPost(() =>
        {
            ghost = player.AttachedEntity!.Value;
            var actionUid = entMan.GetComponent<AdminGhostComponent>(ghost).InteractionToggleActionEntity!.Value;
            action = entMan.GetNetEntity(actionUid);
        });

        return (ghost, action);
    }
}
