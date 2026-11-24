#nullable enable
using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.Gravity;
using Content.Server.Body.Systems;
using ClientHomelanderSystem = Content.Client._Fish.Homelander.HomelanderSystem;
using Content.Server.Physics.Controllers;
using Content.Shared.Movement.Components;
using Content.Shared.Gravity;
using Robust.Shared.Map;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Clothing.Components;
using Content.Shared.Physics;
using Content.Shared.Movement.Events;
using Content.Shared.Stunnable;
using Content.Shared.Speech;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics;
using System.Numerics;
using Content.Server._Fish.Homelander;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Fish.Homelander;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Inventory;
using Content.Shared.Humanoid;
using Content.Shared.Weapons.Reflect;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

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
            var flight = server.EntMan.GetComponent<HomelanderFlightComponent>(patriot);
            var uid = flight.ActionEntity!.Value;
            flightAction = (uid, server.EntMan.GetComponent<ActionComponent>(uid));
            server.PlayerMan.SetAttachedEntity(session, patriot);
        });

        // Ждём, пока клиент получит сущность и привязку управления.
        await Pair.RunTicksSync(10);
        var clientPatriot = Pair.ToClientUid(patriot);
        float speechOffsetBeforeFlight = 0f;
        await client.WaitAssertion(() =>
            speechOffsetBeforeFlight = client.EntMan.GetComponent<SpeechComponent>(clientPatriot).SpeechBubbleOffset);
        var originalMasks = await CheckPhysics(false);
        await server.WaitPost(() => actions.PerformAction(patriot, flightAction));
        await CheckOffset(true);
        var flightMasks = await CheckPhysics(true);
        Assert.That(flightMasks, Is.EqualTo(originalMasks.Select(mask =>
            mask & ~(int) (CollisionGroup.MidImpassable | CollisionGroup.LowImpassable))));
        Assert.That(flightMasks, Is.Not.EqualTo(originalMasks));

        await server.WaitPost(() =>
        {
            server.System<SharedTransformSystem>().SetLocalRotation(patriot, Direction.East.ToAngle());
            server.System<SharedPhysicsSystem>().SetLinearVelocity(patriot, new Vector2(5f, 0f));
        });
        // Даём клиенту применить скорость и направление до проверки спрайта.
        await Pair.RunTicksSync(5);
        await client.WaitAssertion(() =>
            Assert.That(client.EntMan.GetComponent<SpriteComponent>(clientPatriot).Rotation.Theta, Is.Zero.Within(0.001f)));

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
                if (flying)
                {
                    var footstep = server.EntMan.GetComponent<FootstepModifierComponent>(patriot);
                    Assert.That(footstep.FootstepSoundCollection, Is.Null);
                }
                else
                {
                    Assert.That(server.EntMan.HasComponent<FootstepModifierComponent>(patriot), Is.False);
                }
                masks = server.EntMan.GetComponent<FixturesComponent>(patriot).Fixtures.Values
                    .Select(fixture => fixture.CollisionMask).ToArray();
            });
            return masks;
        }

        async Task CheckOffset(bool flying)
        {
            // Даём клиентской анимации подъёма или посадки завершиться.
            await Pair.RunTicksSync(30);
            await client.WaitAssertion(() =>
            {
                var actual = client.EntMan.GetComponent<SpriteComponent>(clientPatriot).Offset;
                var visuals = client.System<ClientHomelanderSystem>();
                var speechOffset = client.EntMan.GetComponent<SpeechComponent>(clientPatriot).SpeechBubbleOffset;
                if (flying)
                {
                    Assert.That(actual.Y, Is.GreaterThanOrEqualTo(HomelanderComponent.FlightVisualLift - 0.001f));
                    Assert.That(speechOffset - speechOffsetBeforeFlight,
                        Is.GreaterThanOrEqualTo(HomelanderComponent.FlightVisualLift - 0.001f));
                    Assert.That(visuals.TryGetFlightPopupOffset(clientPatriot, Angle.Zero, out var popupOffset), Is.True);
                    Assert.That(popupOffset.Y, Is.GreaterThan(speechOffset - speechOffsetBeforeFlight));
                }
                else
                {
                    Assert.That(speechOffset, Is.EqualTo(speechOffsetBeforeFlight));
                    Assert.That(visuals.TryGetFlightPopupOffset(clientPatriot, Angle.Zero, out _), Is.False);
                    Assert.That(actual.Y, Is.InRange(0f, 0.25f));
                    Assert.That(client.EntMan.GetComponent<SpriteComponent>(clientPatriot).Rotation.Theta,
                        Is.Zero.Within(0.001f));
                }
            });
        }
    }

    [Test]
    public async Task OpenSpaceFlightAllowsWeightlessMovement()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var coords = new Robust.Shared.Map.EntityCoordinates(map.MapUid, new Vector2(20f, 20f));
            var patriot = server.EntMan.SpawnEntity("FishMobHomelander", coords);
            Assert.That(server.EntMan.GetComponent<TransformComponent>(patriot).GridUid, Is.Null);

            var flight = server.EntMan.GetComponent<HomelanderFlightComponent>(patriot);
            var action = flight.ActionEntity!.Value;
            var actions = server.System<SharedActionsSystem>();
            Entity<ActionComponent> flightAction = (action, server.EntMan.GetComponent<ActionComponent>(action));
            var events = server.EntMan.EventBus;

            AssertCanMove(false);
            actions.PerformAction(patriot, flightAction);
            AssertCanMove(true);
            actions.PerformAction(patriot, flightAction);
            AssertCanMove(false);

            void AssertCanMove(bool expected)
            {
                var movement = new CanWeightlessMoveEvent(patriot);
                events.RaiseLocalEvent(patriot, ref movement, broadcast: true);
                Assert.That(movement.CanMove, Is.EqualTo(expected));
                Assert.That(movement.UseGroundMovement, Is.EqualTo(expected));
            }
        });
    }

    [Test]
    public async Task FlightInputMovesPatriotInOpenSpace()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();
        var start = new Vector2(20f, 20f);
        var transform = server.System<SharedTransformSystem>();
        EntityUid patriot = default;

        await server.WaitPost(() =>
        {
            patriot = server.EntMan.SpawnEntity("FishMobHomelander", new EntityCoordinates(map.MapUid, start));
            var action = server.EntMan.GetComponent<HomelanderFlightComponent>(patriot).ActionEntity!.Value;
            server.System<SharedActionsSystem>().PerformAction(patriot,
                (action, server.EntMan.GetComponent<ActionComponent>(action)));
            var mover = server.EntMan.GetComponent<InputMoverComponent>(patriot);
            server.System<MoverController>().SetVelocityDirection((patriot, mover), Direction.East, 0, true);
        });

        // Даём персонажу разогнаться выше обычной скорости перемещения в невесомости.
        await server.WaitRunTicks(30);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<TransformComponent>(patriot).GridUid, Is.Null);
            Assert.That(transform.GetWorldPosition(patriot).X - start.X, Is.GreaterThan(0.05f));
            var speed = server.EntMan.GetComponent<MovementSpeedModifierComponent>(patriot);
            var body = server.EntMan.GetComponent<PhysicsComponent>(patriot);
            Assert.That(body.LinearVelocity.X, Is.GreaterThan(speed.WeightlessSprintSpeed));
        });
    }

    [Test]
    public async Task PatriotDoesNotSuffocateInOpenSpace()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();
        EntityUid patriot = default;

        await server.WaitPost(() =>
        {
            patriot = server.EntMan.SpawnEntity("FishMobHomelander",
                new EntityCoordinates(map.MapUid, new Vector2(20f, 20f)));
        });

        // Восемь секунд в вакууме превышают время до удушья у обычного человека.
        await server.WaitRunTicks(server.Timing.TickRate * 8);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<TransformComponent>(patriot).GridUid, Is.Null);
            Assert.That(server.System<RespiratorSystem>().IsBreathing((patriot, null)), Is.True);
        });
    }

    [Test]
    public async Task SpawnsWithArmorAndUsesItsOwnActions()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();
        var actions = server.System<SharedActionsSystem>();
        var appearance = server.System<SharedAppearanceSystem>();
        var inventory = server.System<InventorySystem>();
        var homelander = server.System<HomelanderSystem>();
        var pressure = server.System<BarotraumaSystem>();
        var markings = server.System<SunriseHumanoidMarkingSystem>();

        HomelanderFlightComponent? activeFlight = null;
        StaminaComponent? activeStamina = null;

        await server.WaitAssertion(() =>
        {
            var patriot = server.EntMan.SpawnEntity("FishMobHomelander", map.GridCoords);
            var abilities = server.EntMan.GetComponent<HomelanderComponent>(patriot);
            var flight = server.EntMan.GetComponent<HomelanderFlightComponent>(patriot);
            var stamina = server.EntMan.GetComponent<StaminaComponent>(patriot);

            Assert.That(inventory.TryGetSlotEntity(patriot, "outerClothing", out var suit), Is.True);
            Assert.That(server.EntMan.GetComponent<MetaDataComponent>(suit!.Value).EntityPrototype?.ID,
                Is.EqualTo("FishClothingOuterHardsuitHomelander"));
            Assert.That(server.EntMan.HasComponent<ReflectComponent>(suit.Value), Is.True);
            Assert.That(server.EntMan.HasComponent<ToggleableClothingComponent>(suit.Value), Is.False);
            Assert.That(inventory.TryGetSlotEntity(patriot, "head", out _), Is.False);
            var barotrauma = server.EntMan.GetComponent<BarotraumaComponent>(patriot);
            Assert.That(pressure.GetFeltLowPressure(patriot, barotrauma, 0), Is.EqualTo(Atmospherics.OneAtmosphere));
            Assert.That(pressure.GetFeltHighPressure(patriot, barotrauma, 10000f), Is.EqualTo(Atmospherics.OneAtmosphere));
            Assert.That(inventory.TryUnequip(patriot, "outerClothing", force: true), Is.True);
            var costume = server.EntMan.SpawnEntity("FishClothingOuterCostumeHomelander", map.GridCoords);
            Assert.That(inventory.TryEquip(patriot, costume, "outerClothing", force: true), Is.True);
            Assert.That(pressure.GetFeltLowPressure(patriot, barotrauma, 0), Is.Zero);
            Assert.That(pressure.GetFeltHighPressure(patriot, barotrauma, 10000f), Is.EqualTo(10000f));
            Assert.That(server.EntMan.HasComponent<ReflectComponent>(costume), Is.False);
            Assert.That(abilities.Laser, Is.Not.Null);
            Assert.That(markings.TryGetLayerMarkings(patriot, HumanoidVisualLayers.Hair, out var hair), Is.True);
            Assert.That(hair[0].MarkingId.Id, Is.EqualTo("HumanHairCombed"));

            var mapSystem = server.System<SharedMapSystem>();
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, 0), map.Tile.Tile);
            var gravity = server.EntMan.EnsureComponent<GravityComponent>(map.Grid.Owner);
            server.System<GravitySystem>().EnableGravity(map.Grid.Owner, gravity);
            var nearby = server.EntMan.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            Assert.That(server.System<SharedGravitySystem>().IsWeightless(nearby), Is.False);
            var distant = server.EntMan.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(6, 0)));
            var scream = GetAction(abilities.ScreamAction);
            actions.PerformAction(patriot, scream, predicted: false);
            Assert.That(server.EntMan.GetComponent<InstantActionComponent>(scream).Event?.Handled, Is.True);
            Assert.That(server.EntMan.HasComponent<KnockedDownComponent>(nearby), Is.True);
            Assert.That(server.EntMan.HasComponent<KnockedDownComponent>(distant), Is.False);
            Assert.That(server.EntMan.HasComponent<KnockedDownComponent>(patriot), Is.False);

            var laserTarget = map.GridCoords.Offset(new Vector2(4, 0));
            Assert.That(homelander.TryFireLaser((patriot, abilities), laserTarget), Is.False);
            var laser = GetAction(abilities.LaserAction);
            var contentEye = server.EntMan.GetComponent<ContentEyeComponent>(patriot);
            var eye = server.EntMan.GetComponent<EyeComponent>(patriot);
            var previousZoom = contentEye.TargetZoom;
            var previousPvsScale = eye.PvsScale;
            var crusher = server.EntMan.SpawnEntity("WeaponCrusher", map.GridCoords);
            AssertSecondaryShotCancelled(false);
            actions.PerformAction(patriot, laser);
            Assert.That(appearance.TryGetData<bool>(patriot, HomelanderVisuals.LaserActive, out var active) && active,
                Is.True);

            AssertSecondaryShotCancelled(true);
            Assert.That(contentEye.TargetZoom.X, Is.GreaterThan(previousZoom.X));
            Assert.That(eye.PvsScale, Is.GreaterThan(previousPvsScale));
            Assert.That(homelander.TryFireLaser((patriot, abilities), laserTarget), Is.True);

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
            AssertSecondaryShotCancelled(false);
            Assert.That(contentEye.TargetZoom, Is.EqualTo(previousZoom));
            Assert.That(eye.PvsScale, Is.EqualTo(previousPvsScale));

            void AssertSecondaryShotCancelled(bool expected)
            {
                var shot = new ShotAttemptedEvent
                {
                    User = patriot,
                    Used = (crusher, server.EntMan.GetComponent<GunComponent>(crusher)),
                };
                server.EntMan.EventBus.RaiseLocalEvent(patriot, ref shot, broadcast: false);
                Assert.That(shot.Cancelled, Is.EqualTo(expected));
            }

            Entity<ActionComponent> GetAction(EntityUid? action)
            {
                Assert.That(action, Is.Not.Null);
                return (action!.Value, server.EntMan.GetComponent<ActionComponent>(action.Value));
            }
        });

        // За это время обычный полёт успел бы накопить расход выносливости.
        await server.WaitRunTicks(120);
        await server.WaitAssertion(() =>
        {
            Assert.That(activeFlight!.FlightEnabled, Is.True);
            Assert.That(activeStamina!.StaminaDamage, Is.Zero);
        });
    }
}
