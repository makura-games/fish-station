using System.Numerics;
using Content.Shared._Fish.Mobs.Mimic;
using Content.Shared.Actions.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Fish.Mobs;

[TestFixture]
public sealed class MimicJumpTest
{
    private const string JumpActionId = "FishActionMimicJump";
    private const int SettleTicks = 5;
    private const int FlightTicks = 30;

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

        await server.WaitPost(() =>
        {
            mimic = entMan.SpawnEntity("MobMimic", spawn);
            user = entMan.SpawnEntity("MobCorgiMouse", spawn);
        });

        await server.WaitRunTicks(SettleTicks);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<SleepingComponent>(mimic), Is.True, "Мимик должен изначально спать");
            Assert.That(entMan.HasComponent<StunnedComponent>(mimic), Is.True, "Спящий мимик должен быть оглушён");
            Assert.That(entMan.GetComponent<InputMoverComponent>(mimic).CanMove, Is.False);
            Assert.That(HasJumpAction(entMan, mimic), Is.False);
        });

        await server.WaitPost(() =>
        {
            entMan.EventBus.RaiseLocalEvent(mimic, new InteractHandEvent(user, mimic));
        });

        await server.WaitRunTicks(SettleTicks);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<SleepingComponent>(mimic), Is.False, "Мимик должен проснуться после нажатия");
            Assert.That(entMan.GetComponent<InputMoverComponent>(mimic).CanMove, Is.True);
            Assert.That(HasJumpAction(entMan, mimic), Is.True);
        });

        await pair.CleanReturnAsync();
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

        await server.WaitPost(() =>
        {
            mimic = entMan.SpawnEntity("MobMimic", spawn);
            user = entMan.SpawnEntity("MobCorgiMouse", spawn);
            entMan.EventBus.RaiseLocalEvent(mimic, new InteractHandEvent(user, mimic));
        });

        await server.WaitRunTicks(SettleTicks);

        var startX = 0f;
        MimicJumpActionEvent ev = null!;

        await server.WaitPost(() =>
        {
            startX = xformSys.GetWorldPosition(mimic).X;

            ev = new MimicJumpActionEvent
            {
                Performer = mimic,
                Target = new EntityCoordinates(mimic, new Vector2(5f, 0f)),
            };

            entMan.EventBus.RaiseLocalEvent(mimic, (object) ev, true);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(ev.Handled, Is.True, "Событие прыжка должно обрабатываться системой мимика");
            Assert.That(entMan.GetComponent<MimicJumpComponent>(mimic).Jumping, Is.True, "Прыжок должен быть активным");
        });

        await server.WaitRunTicks(FlightTicks);

        await server.WaitAssertion(() =>
        {
            var endX = xformSys.GetWorldPosition(mimic).X;
            Assert.That(endX, Is.GreaterThan(startX + 0.3f), "Мимик должен сместиться в сторону прыжка");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MimicJumpStunsTarget()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var damageSys = entMan.System<DamageableSystem>();

        var map = await pair.CreateTestMap();
        var spawn = new EntityCoordinates(map.Grid, 0.5f, 0.5f);

        EntityUid mimic = default;
        EntityUid user = default;
        EntityUid target = default;

        await server.WaitPost(() =>
        {
            mimic = entMan.SpawnEntity("MobMimic", spawn);
            user = entMan.SpawnEntity("MobCorgiMouse", spawn);
            target = entMan.SpawnEntity("MobCorgiMouse", spawn);
            entMan.EventBus.RaiseLocalEvent(mimic, new InteractHandEvent(user, mimic));
        });

        await server.WaitRunTicks(SettleTicks);

        var damageBefore = FixedPoint2.Zero;

        await server.WaitPost(() =>
        {
            var jump = new MimicJumpActionEvent
            {
                Performer = mimic,
                Target = new EntityCoordinates(mimic, new Vector2(5f, 0f)),
            };

            entMan.EventBus.RaiseLocalEvent(mimic, (object) jump, true);

#pragma warning disable CS0618
            damageBefore = damageSys.GetTotalDamage((target, entMan.GetComponent<DamageableComponent>(target)));
#pragma warning restore CS0618

            var hit = new ThrowDoHitEvent(mimic, target, entMan.GetComponent<ThrownItemComponent>(mimic));
            entMan.EventBus.RaiseLocalEvent(mimic, ref hit, true);
        });

        await server.WaitRunTicks(SettleTicks);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<StunnedComponent>(target), Is.True,
                "Цель, задетая прыжком мимика, должна пасть в стан");

#pragma warning disable CS0618
            var damageAfter = damageSys.GetTotalDamage((target, entMan.GetComponent<DamageableComponent>(target)));
#pragma warning restore CS0618
            Assert.That(damageAfter, Is.GreaterThan(damageBefore), "Цель, задетая прыжком, должна получить урон");
        });

        await pair.CleanReturnAsync();
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
