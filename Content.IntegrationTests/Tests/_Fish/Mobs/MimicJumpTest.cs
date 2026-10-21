using System.Numerics;
using Content.Shared._Fish.Mobs.Mimic;
using Content.Shared.Actions.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Fish.Mobs;

/// <summary>
/// Проверяет, что мимик изначально спит и просыпается по нажатию,
/// а после пробуждения умеет прыгать и оглушает задетую цель.
/// </summary>
[TestFixture]
public sealed class MimicJumpTest
{
    private const string JumpActionId = "ActionMimicJump";

    [Test]
    public async Task MimicStartsAsleepAndWakesUpOnInteract()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var map = await pair.CreateTestMap();
        var spawn = new EntityCoordinates(map.Grid, 0.5f, 0.5f);

        EntityUid mimic = default;
        EntityUid user = default;

        await server.WaitAssertion(() =>
        {
            mimic = entMan.SpawnEntity("MobMimic", spawn);
            user = entMan.SpawnEntity("MobCorgiMouse", spawn);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            // Спит: не двигается и не имеет действий.
            Assert.That(entMan.HasComponent<SleepingComponent>(mimic), Is.True, "Мимик должен изначально спать");
            Assert.That(entMan.HasComponent<StunnedComponent>(mimic), Is.True, "Спящий мимик должен быть оглушён");
            Assert.That(entMan.GetComponent<InputMoverComponent>(mimic).CanMove, Is.False);
            Assert.That(HasJumpAction(entMan, mimic), Is.False);

            // Нажали на мимика.
            entMan.EventBus.RaiseLocalEvent(mimic, new InteractHandEvent(user, mimic));
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            // Проснулся: сон снят, мимик двигается и получил действие прыжка.
            Assert.That(entMan.HasComponent<SleepingComponent>(mimic), Is.False, "Мимик должен проснуться после нажатия");
            Assert.That(entMan.GetComponent<InputMoverComponent>(mimic).CanMove, Is.True);
            Assert.That(HasJumpAction(entMan, mimic), Is.True);
        });
    }

    [Test]
    public async Task MimicJumpMovesMob()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var xformSys = entMan.System<SharedTransformSystem>();

        var map = await pair.CreateTestMap();
        var spawn = new EntityCoordinates(map.Grid, 0.5f, 0.5f);

        EntityUid mimic = default;
        EntityUid user = default;

        await server.WaitAssertion(() =>
        {
            mimic = entMan.SpawnEntity("MobMimic", spawn);
            user = entMan.SpawnEntity("MobCorgiMouse", spawn);
            entMan.EventBus.RaiseLocalEvent(mimic, new InteractHandEvent(user, mimic));
        });

        await server.WaitRunTicks(5);

        var startX = 0f;

        await server.WaitAssertion(() =>
        {
            startX = xformSys.GetWorldPosition(mimic).X;

            // Прыжок в сторону +X.
            var ev = new MimicJumpActionEvent
            {
                Performer = mimic,
                Target = new EntityCoordinates(mimic, new Vector2(5f, 0f)),
            };

            entMan.EventBus.RaiseLocalEvent(mimic, (object) ev, true);

            Assert.That(ev.Handled, Is.True, "Событие прыжка должно обрабатываться системой мимика");
        });

        await server.WaitRunTicks(30);

        await server.WaitAssertion(() =>
        {
            var endX = xformSys.GetWorldPosition(mimic).X;
            Assert.That(endX, Is.GreaterThan(startX + 0.3f), "Мимик должен сместиться в сторону прыжка");
        });
    }

    [Test]
    public async Task MimicJumpStunsTarget()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var map = await pair.CreateTestMap();
        var spawn = new EntityCoordinates(map.Grid, 0.5f, 0.5f);

        EntityUid mimic = default;
        EntityUid user = default;
        EntityUid target = default;

        await server.WaitAssertion(() =>
        {
            mimic = entMan.SpawnEntity("MobMimic", spawn);
            user = entMan.SpawnEntity("MobCorgiMouse", spawn);
            target = entMan.SpawnEntity("MobCorgiMouse", spawn);
            entMan.EventBus.RaiseLocalEvent(mimic, new InteractHandEvent(user, mimic));
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            // Мимик влетел в цель при прыжке.
            var ev = new ThrowDoHitEvent(mimic, target, new ThrownItemComponent());
            entMan.EventBus.RaiseLocalEvent(mimic, ref ev, true);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<StunnedComponent>(target), Is.True,
                "Цель, задетая прыжком мимика, должна пасть в стан");
        });
    }

    private static bool HasJumpAction(IEntityManager entMan, EntityUid uid)
    {
        var actions = entMan.GetComponent<ActionsComponent>(uid);

        foreach (var actionUid in actions.Actions)
        {
            var meta = entMan.GetComponent<MetaDataComponent>(actionUid);
            if (meta.EntityPrototype?.ID == JumpActionId)
                return true;
        }

        return false;
    }
}
