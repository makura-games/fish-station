using System;
using System.Linq;
using Content.Server.Mech.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.CombatMode;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Light.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Fish.Mech;

[TestFixture, NonParallelizable]
public sealed class MechBrainTest
{
    [Test]
    public async Task MechPilotCannotInteractWithPoweredLights()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid mech = default;
        EntityUid brain = default;
        EntityUid light = default;
        var ticksToWait = 0;
        var gameTiming = server.ResolveDependency<IGameTiming>();

        await server.WaitPost(() =>
        {
            mech = entMan.SpawnEntity("MechRipleyBattery", map.GridCoords);
            brain = entMan.SpawnEntity("PositronicBrain", map.GridCoords);
            light = entMan.SpawnEntity("PoweredLightPostSmall", map.GridCoords);
        });

        await pair.RunTicksSync(5);

        var inserted = false;
        EntityUid? bulb = null;
        await server.WaitPost(() =>
        {
            inserted = entMan.System<MechSystem>().TryInsert(mech, brain);
            var lightComponent = entMan.GetComponent<PoweredLightComponent>(light);
            bulb = lightComponent.LightBulbContainer.ContainedEntity;
            ticksToWait = (int) Math.Ceiling(lightComponent.EjectBulbDelay * gameTiming.TickRate) + 1;
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(inserted, Is.True);
            Assert.That(bulb, Is.Not.Null);
        });

        await server.WaitPost(() =>
            entMan.System<SharedInteractionSystem>().UserInteraction(
                brain,
                entMan.GetComponent<TransformComponent>(light).Coordinates,
                light));

        // Ждём полный DoAfter извлечения лампы и один тик обработки события.
        await pair.RunTicksSync(ticksToWait);

        await server.WaitAssertion(() =>
        {
            var lightComponent = entMan.GetComponent<PoweredLightComponent>(light);
            Assert.That(lightComponent.LightBulbContainer.ContainedEntity, Is.EqualTo(bulb));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlacklistedHeldBrainStillAllowsEnteringMech()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid user = default;
        EntityUid mech = default;
        EntityUid brain = default;
        var hasEnterVerb = false;
        var pickedUp = false;

        await server.WaitPost(() =>
        {
            user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            mech = entMan.SpawnEntity("MechRipleyBattery", map.GridCoords);
            brain = entMan.SpawnEntity("PositronicBrain", map.GridCoords);
            entMan.GetComponent<MechComponent>(mech).PilotBlacklist = new EntityWhitelist { Components = ["BorgBrain"] };
            pickedUp = entMan.System<SharedHandsSystem>().TryPickupAnyHand(user, brain);

            if (pickedUp)
            {
                var hands = entMan.GetComponent<HandsComponent>(user);
                var verbs = new GetVerbsEvent<AlternativeVerb>(user, mech, brain, hands, true, true, true, []);
                entMan.EventBus.RaiseLocalEvent(mech, verbs);

                var enterText = server.ResolveDependency<ILocalizationManager>().GetString("mech-verb-enter");
                hasEnterVerb = verbs.Verbs.Any(verb => verb.Text == enterText);
            }
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(pickedUp, Is.True);
            Assert.That(hasEnterVerb, Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("PositronicBrain", false)]
    [TestCase("PositronicBrain", true)]
    [TestCase("PersonalAI", false)]
    [TestCase("MMI", false)]
    public async Task HeldBrainRespectsPilotBlacklist(string prototype, bool blacklisted)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid user = default;
        EntityUid mech = default;
        EntityUid brain = default;
        var pickedUp = false;

        await server.WaitPost(() =>
        {
            user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            mech = entMan.SpawnEntity("MechRipleyBattery", map.GridCoords);
            brain = entMan.SpawnEntity(prototype, map.GridCoords);
            var component = entMan.GetComponent<MechComponent>(mech);
            if (blacklisted)
                component.PilotBlacklist = new EntityWhitelist { Components = ["BorgBrain"] };

            pickedUp = entMan.System<SharedHandsSystem>().TryPickupAnyHand(user, brain);
        });

        await server.WaitAssertion(() => Assert.That(pickedUp, Is.True));
        await server.WaitPost(() =>
            entMan.System<SharedInteractionSystem>().InteractUsing(user, brain, mech, map.GridCoords));

        await server.WaitAssertion(() =>
        {
            var pilot = entMan.GetComponent<MechComponent>(mech).PilotSlot.ContainedEntity;
            Assert.That(pilot, blacklisted ? Is.Null : Is.EqualTo(brain));
            Assert.That(entMan.HasComponent<MechPilotComponent>(brain), Is.EqualTo(!blacklisted));
        });

        await server.WaitPost(() => entMan.System<MechSystem>().TryEject(mech));
        await pair.CleanReturnAsync();
    }

    [TestCase(false, true)]
    [TestCase(true, true)]
    [TestCase(false, false)]
    [TestCase(true, false)]
    public async Task EjectionRestoresBrainInteractionState(bool hadCombatMode, bool hadBlockMovement)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid mech = default;
        EntityUid brain = default;

        await server.WaitPost(() =>
        {
            mech = entMan.SpawnEntity("MechRipleyBattery", map.GridCoords);
            brain = entMan.SpawnEntity("PositronicBrain", map.GridCoords);
            if (hadBlockMovement)
                entMan.GetComponent<BlockMovementComponent>(brain).BlockInteraction = true;
            else
                entMan.RemoveComponent<BlockMovementComponent>(brain);
            entMan.EnsureComponent<InputMoverComponent>(brain);
            if (hadCombatMode)
                entMan.EnsureComponent<CombatModeComponent>(brain);
        });

        for (var cycle = 0; cycle < 2; cycle++)
        {
            var inserted = false;
            await server.WaitPost(() => inserted = entMan.System<MechSystem>().TryInsert(mech, brain));
            await server.WaitAssertion(() =>
            {
                Assert.That(inserted, Is.True);
                Assert.That(entMan.GetComponent<BlockMovementComponent>(brain).BlockInteraction, Is.False);
                Assert.That(entMan.HasComponent<CombatModeComponent>(brain), Is.True);
                Assert.That(entMan.GetComponent<CombatModeComponent>(brain).IsInCombatMode, Is.False);
            });

            await server.WaitPost(() => entMan.System<SharedCombatModeSystem>().SetInCombatMode(brain, true));
            var ejected = false;
            await server.WaitPost(() => ejected = entMan.System<MechSystem>().TryEject(mech));
            await server.WaitAssertion(() =>
            {
                Assert.That(ejected, Is.True);
                Assert.That(entMan.HasComponent<BlockMovementComponent>(brain), Is.EqualTo(hadBlockMovement));
                if (hadBlockMovement)
                    Assert.That(entMan.GetComponent<BlockMovementComponent>(brain).BlockInteraction, Is.True);
                Assert.That(entMan.HasComponent<CombatModeComponent>(brain), Is.EqualTo(hadCombatMode));
                if (hadCombatMode)
                    Assert.That(entMan.GetComponent<CombatModeComponent>(brain).IsInCombatMode, Is.False);
                Assert.That(entMan.System<ActionBlockerSystem>().CanMove(brain), Is.EqualTo(!hadBlockMovement));
                Assert.That(entMan.HasComponent<MechPilotComponent>(brain), Is.False);
                Assert.That(entMan.HasComponent<RelayInputMoverComponent>(brain), Is.False);
            });
        }

        await pair.CleanReturnAsync();
    }
}
