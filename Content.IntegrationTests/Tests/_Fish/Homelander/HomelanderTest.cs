using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Clothing.Components;
using Content.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics;
using System.Numerics;
using Content.Server.Weapons.Ranged.Systems;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Fish.Homelander;
using Content.Shared._Sunrise.Abilities.Milira;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Inventory;
using Content.Shared.Humanoid;
using Content.Shared.Weapons.Reflect;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Fish.Homelander;

[TestFixture]
public sealed class HomelanderTest : GameTest
{
    [Test]
    public async Task FlightRaisesSpriteAndLandingRestoresIt()
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var map = await Pair.CreateTestMap();
        var session = Pair.Player!;
        var previousEntity = session.AttachedEntity;
        var actions = server.System<SharedActionsSystem>();
        EntityUid patriot = default;
        Entity<ActionComponent> flightAction = default;

        await server.WaitPost(() =>
        {
            patriot = server.EntMan.SpawnEntity("FishMobHomelander", map.GridCoords);
            var flight = server.EntMan.GetComponent<WingFlightComponent>(patriot);
            var uid = flight.ActionEntity!.Value;
            flightAction = (uid, server.EntMan.GetComponent<ActionComponent>(uid));
            server.PlayerMan.SetAttachedEntity(session, patriot);
        });

        await Pair.RunTicksSync(10);
        var clientPatriot = Pair.ToClientUid(patriot);
        var originalMasks = await CheckPhysics(false);
        await server.WaitPost(() => actions.PerformAction(patriot, flightAction));
        await CheckOffset(true);
        var flightMasks = await CheckPhysics(true);
        Assert.That(flightMasks, Is.EqualTo(originalMasks.Select(mask =>
            mask & ~(int) (CollisionGroup.MidImpassable | CollisionGroup.LowImpassable))));
        Assert.That(flightMasks, Is.Not.EqualTo(originalMasks));

        await server.WaitPost(() => actions.PerformAction(patriot, flightAction));
        await CheckOffset(false);
        Assert.That(await CheckPhysics(false), Is.EqualTo(originalMasks));

        await server.WaitPost(() => server.PlayerMan.SetAttachedEntity(session, previousEntity));

        async Task<int[]> CheckPhysics(bool flying)
        {
            int[] masks = [];
            await server.WaitAssertion(() =>
            {
                var body = server.EntMan.GetComponent<PhysicsComponent>(patriot);
                Assert.That(body.BodyStatus, Is.EqualTo(flying ? BodyStatus.InAir : BodyStatus.OnGround));
                masks = server.EntMan.GetComponent<FixturesComponent>(patriot).Fixtures.Values
                    .Select(fixture => fixture.CollisionMask).ToArray();
            });
            return masks;
        }

        async Task CheckOffset(bool flying)
        {
            await Pair.RunTicksSync(30);
            await client.WaitAssertion(() =>
            {
                var actual = client.EntMan.GetComponent<SpriteComponent>(clientPatriot).Offset;
                if (flying)
                    Assert.That(actual.Y, Is.GreaterThanOrEqualTo(HomelanderComponent.FlightVisualLift - 0.001f));
                else
                    Assert.That(actual.Y, Is.InRange(0f, 0.25f));
            });
        }
    }

    [Test]
    public async Task SpawnsWithArmorAndUsesItsOwnActions()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();
        var actions = server.System<SharedActionsSystem>();
        var appearance = server.System<SharedAppearanceSystem>();
        var inventory = server.System<InventorySystem>();
        var guns = server.System<GunSystem>();
        var pressure = server.System<BarotraumaSystem>();
        var markings = server.System<SunriseHumanoidMarkingSystem>();

        WingFlightComponent? activeFlight = null;
        StaminaComponent? activeStamina = null;

        await server.WaitAssertion(() =>
        {
            var patriot = server.EntMan.SpawnEntity("FishMobHomelander", map.GridCoords);
            var abilities = server.EntMan.GetComponent<HomelanderComponent>(patriot);
            var flight = server.EntMan.GetComponent<WingFlightComponent>(patriot);
            var stamina = server.EntMan.GetComponent<StaminaComponent>(patriot);

            Assert.That(inventory.TryGetSlotEntity(patriot, "outerClothing", out var suit), Is.True);
            Assert.That(server.EntMan.GetComponent<MetaDataComponent>(suit!.Value).EntityPrototype?.ID,
                Is.EqualTo("FishClothingOuterHardsuitHomelander"));
            Assert.That(server.EntMan.HasComponent<ReflectComponent>(suit.Value), Is.True);
            Assert.That(server.EntMan.HasComponent<ToggleableClothingComponent>(suit.Value), Is.False);
            Assert.That(inventory.TryGetSlotEntity(patriot, "head", out _), Is.False);
            var barotrauma = server.EntMan.GetComponent<BarotraumaComponent>(patriot);
            Assert.That(pressure.GetFeltLowPressure(patriot, barotrauma, 0), Is.GreaterThan(100f));
            Assert.That(inventory.TryUnequip(patriot, "outerClothing", force: true), Is.True);
            var costume = server.EntMan.SpawnEntity("FishClothingOuterCostumeHomelander", map.GridCoords);
            Assert.That(inventory.TryEquip(patriot, costume, "outerClothing", force: true), Is.True);
            Assert.That(pressure.GetFeltLowPressure(patriot, barotrauma, 0), Is.Zero);
            Assert.That(server.EntMan.HasComponent<ReflectComponent>(costume), Is.False);
            Assert.That(abilities.Laser, Is.Not.Null);
            Assert.That(markings.TryGetLayerMarkings(patriot, HumanoidVisualLayers.Hair, out var hair), Is.True);
            Assert.That(hair[0].MarkingId.Id, Is.EqualTo("HumanHairCombed"));

            var scream = GetAction(abilities.ScreamAction);
            actions.PerformAction(patriot, scream, predicted: false);
            Assert.That(server.EntMan.GetComponent<InstantActionComponent>(scream).Event?.Handled, Is.True);

            var laser = GetAction(abilities.LaserAction);
            actions.PerformAction(patriot, laser);
            Assert.That(appearance.TryGetData<bool>(patriot, HomelanderVisuals.LaserActive, out var active) && active,
                Is.True);

            var emitter = abilities.Laser!.Value;
            var gun = server.EntMan.GetComponent<GunComponent>(emitter);
            Assert.That(guns.AttemptShoot(patriot, emitter, gun, map.GridCoords.Offset(new Vector2(4, 0))), Is.True);

            var previousStaminaDamage = stamina.StaminaDamage;
            actions.PerformAction(patriot, GetAction(flight.ActionEntity));
            Assert.That(flight.FlightEnabled, Is.True);
            Assert.That(stamina.StaminaDamage, Is.EqualTo(previousStaminaDamage));
            activeFlight = flight;
            activeStamina = stamina;

            actions.PerformAction(patriot, laser);
            Assert.That(appearance.TryGetData<bool>(patriot, HomelanderVisuals.LaserActive, out active) && active,
                Is.False);
            Assert.That(flight.FlightEnabled, Is.True);

            Entity<ActionComponent> GetAction(EntityUid? action)
            {
                Assert.That(action, Is.Not.Null);
                return (action!.Value, server.EntMan.GetComponent<ActionComponent>(action.Value));
            }
        });

        await server.WaitRunTicks(120);
        await server.WaitAssertion(() =>
        {
            Assert.That(activeFlight!.FlightEnabled, Is.True);
            Assert.That(activeStamina!.StaminaDamage, Is.Zero);
        });
    }
}
