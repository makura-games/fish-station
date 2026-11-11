using System.Numerics;
using Content.Server.Weapons.Ranged.Systems;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Fish.Homelander;
using Content.Shared._Sunrise.Abilities.Milira;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Reflect;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Fish.Homelander;

[TestFixture]
public sealed class HomelanderTest : GameTest
{
    [Test]
    public async Task SpawnsWithArmorAndUsesItsOwnActions()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();
        var actions = server.System<SharedActionsSystem>();
        var appearance = server.System<SharedAppearanceSystem>();
        var inventory = server.System<InventorySystem>();
        var guns = server.System<GunSystem>();

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
            Assert.That(abilities.Laser, Is.Not.Null);

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
            Assert.That(stamina.StaminaDamage - previousStaminaDamage, Is.GreaterThan(10f));

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
    }
}
