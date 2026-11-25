#nullable enable
using Content.Shared.CombatMode;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Fish.Cuffs;

[TestFixture]
[TestOf(typeof(SharedCuffableSystem))]
public sealed class FishCuffBreakoutPushInterruptTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: FishCuffBreakoutDummy
  name: FishCuffBreakoutDummy
  components:
  - type: Cuffable
  - type: Hands
    hands:
      hand_right:
        location: Right
      hand_left:
        location: Left
    sortedHands:
    - hand_right
    - hand_left
  - type: ComplexInteraction
  - type: DoAfter

- type: entity
  id: FishHandcuffsDummy
  name: FishHandcuffsDummy
  components:
  - type: Handcuff
  - type: UseDelay
    delay: 0.1
";

    [Test]
    public async Task HandcuffBreakoutTime_DefaultIsSixtySeconds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var cuffs = entMan.SpawnEntity("Handcuffs", MapCoordinates.Nullspace);
            Assert.That(entMan.TryGetComponent(cuffs, out HandcuffComponent? cuff), Is.True);
            Assert.That(cuff!.BreakoutTime, Is.EqualTo(60f));
            entMan.DeleteEntity(cuffs);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SuccessfulPush_InterruptsSelfBreakoutDoAfter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var cuffable = entMan.System<SharedCuffableSystem>();
        var doAfter = entMan.System<SharedDoAfterSystem>();

        EntityUid target = default;
        EntityUid pusher = default;
        EntityUid cuffs = default;

        await server.WaitPost(() =>
        {
            target = entMan.SpawnEntity("FishCuffBreakoutDummy", map.GridCoords);
            pusher = entMan.SpawnEntity("FishCuffBreakoutDummy", map.GridCoords);
            cuffs = entMan.SpawnEntity("FishHandcuffsDummy", map.GridCoords);

            Assert.That(cuffable.TryAddNewCuffs(target, pusher, cuffs), Is.True);
            Assert.That(cuffable.IsCuffed(target), Is.True);

            cuffable.TryUncuff(target, target);
            var cuffComp = entMan.GetComponent<CuffableComponent>(target);
            Assert.That(cuffComp.SelfBreakoutDoAfter, Is.Not.Null);
            Assert.That(doAfter.IsRunning(cuffComp.SelfBreakoutDoAfter), Is.True);

            // Успешный пуш: DisarmedEvent с Handled (как после stamina/hands).
            var ev = new DisarmedEvent(target, pusher, 1f) { Handled = true };
            entMan.EventBus.RaiseLocalEvent(target, ref ev);

            cuffComp = entMan.GetComponent<CuffableComponent>(target);
            Assert.That(doAfter.IsRunning(cuffComp.SelfBreakoutDoAfter), Is.False);
            Assert.That(cuffComp.SelfBreakoutDoAfter, Is.Null);
            Assert.That(cuffComp.BreakoutInterruptedByPush, Is.False);
            Assert.That(cuffable.IsCuffed(target), Is.True, "Прерывание не снимает наручники");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnhandledDisarm_DoesNotInterruptBreakout()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var cuffable = entMan.System<SharedCuffableSystem>();
        var doAfter = entMan.System<SharedDoAfterSystem>();

        await server.WaitPost(() =>
        {
            var target = entMan.SpawnEntity("FishCuffBreakoutDummy", map.GridCoords);
            var pusher = entMan.SpawnEntity("FishCuffBreakoutDummy", map.GridCoords);
            var cuffs = entMan.SpawnEntity("FishHandcuffsDummy", map.GridCoords);

            Assert.That(cuffable.TryAddNewCuffs(target, pusher, cuffs), Is.True);
            cuffable.TryUncuff(target, target);

            var cuffComp = entMan.GetComponent<CuffableComponent>(target);
            Assert.That(doAfter.IsRunning(cuffComp.SelfBreakoutDoAfter), Is.True);

            var ev = new DisarmedEvent(target, pusher, 1f) { Handled = false };
            entMan.EventBus.RaiseLocalEvent(target, ref ev);

            cuffComp = entMan.GetComponent<CuffableComponent>(target);
            Assert.That(doAfter.IsRunning(cuffComp.SelfBreakoutDoAfter), Is.True);
            Assert.That(cuffComp.SelfBreakoutDoAfter, Is.Not.Null);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HelperUncuff_IsNotTrackedAsSelfBreakout()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var cuffable = entMan.System<SharedCuffableSystem>();

        await server.WaitPost(() =>
        {
            var target = entMan.SpawnEntity("FishCuffBreakoutDummy", map.GridCoords);
            var helper = entMan.SpawnEntity("FishCuffBreakoutDummy", map.GridCoords);
            var cuffs = entMan.SpawnEntity("FishHandcuffsDummy", map.GridCoords);

            Assert.That(cuffable.TryAddNewCuffs(target, helper, cuffs), Is.True);
            cuffable.TryUncuff(target, helper);

            var cuffComp = entMan.GetComponent<CuffableComponent>(target);
            Assert.That(cuffComp.SelfBreakoutDoAfter, Is.Null);
        });

        await pair.CleanReturnAsync();
    }
}
