using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server._Fish.GridWelding;
using Content.Server.Decals;
using Content.Server.Salvage.Magnet;
using Content.Shared.ActionBlocker;
using Content.Shared.FixedPoint;
using Content.Shared.Friction;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Pinpointer;
using Content.Shared.Stunnable;
using Content.Shared.Tools.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests._Fish.GridWelding;

[TestFixture]
public sealed class GridWeldingRegressionTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: FishGridWeldingTestWelder
          parent: Welder
          suffix: Fish
          components:
          - type: Welder
            fuelConsumption: 0

        - type: entity
          id: FishGridWeldingExistingBeacon
          name: existing beacon
          suffix: Fish
          components:
          - type: NavMapBeacon
            text: Existing beacon

        - type: entity
          id: FishGridWeldingIncomingBeacon
          name: incoming beacon
          suffix: Fish
          components:
          - type: NavMapBeacon
            text: Incoming beacon
        """;

    /// <summary>Ensures instant actors release their completed welding state and can immediately select another seam.</summary>
    [Test]
    public async Task InstantWeldingClearsTheActionAndCanWeldAgain()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var entities = server.EntMan;
        WeldingScene scene = default;
        var started = false;

        await server.WaitPost(() =>
        {
            scene = CreateScene(pair);
            started = StartWelding(entities, scene);
        });
        await server.WaitAssertion(() =>
        {
            AssertReady(scene);
            Assert.That(started, Is.True);
        });
        await WaitForMerge(pair, scene.Second);
        await server.WaitAssertion(() => AssertSelectionCleared(entities, scene.Welder));

        EntityUid third = default;
        await server.WaitPost(() =>
        {
            var maps = entities.System<SharedMapSystem>();
            var grid = maps.CreateGridEntity(entities.GetComponent<TransformComponent>(scene.First).MapID);
            third = grid.Owner;
            maps.SetTile(grid, new Vector2i(2, 0), Plating(pair));
            var transform = entities.System<SharedTransformSystem>();
            transform.SetWorldPositionRotation(third, transform.GetWorldPosition(scene.First),
                transform.GetWorldRotation(scene.First));
            var welding = entities.System<GridWeldingSystem>();
            started = welding.TrySelectTile((scene.Welder, null), scene.User, new EntityCoordinates(scene.First, 1.5f, 0.5f)) &&
                      welding.TrySelectTile((scene.Welder, null), scene.User, new EntityCoordinates(third, 2.5f, 0.5f));
        });
        await server.WaitAssertion(() => Assert.That(started, Is.True));
        await WaitForMerge(pair, third);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(scene.First), Is.True);
            AssertSelectionCleared(entities, scene.Welder);
        });
        await server.WaitPost(() => entities.DeleteEntity(scene.Map));
        await pair.CleanReturnAsync();
    }

    /// <summary>Protects wreck grids tracked by an active salvage magnet, including when its list also contains a stale UID.</summary>
    [TestCase(false)]
    [TestCase(true)]
    public async Task ActiveMagnetTargetCannotBeEitherWeldingInput(bool targetFirst)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var entities = server.EntMan;
        WeldingScene scene = default;
        FixedPoint2 fuel = default;
        var started = false;

        await server.WaitPost(() =>
        {
            scene = CreateScene(pair);
            var station = entities.SpawnEntity(null, new EntityCoordinates(scene.Map, -5, -5));
            var stale = entities.SpawnEntity(null, new EntityCoordinates(scene.Map, -5, -5));
            entities.DeleteEntity(stale);
            var magnet = entities.AddComponent<SalvageMagnetDataComponent>(station);
            magnet.ActiveEntities = [stale, targetFirst ? scene.First : scene.Second];
            magnet.EndTime = server.Timing.CurTime + TimeSpan.FromMinutes(5);
            magnet.NextOffer = server.Timing.CurTime + TimeSpan.FromMinutes(10);
            fuel = entities.System<SharedToolSystem>().GetWelderFuelAndCapacity(scene.Welder).fuel;
            started = StartWelding(entities, scene);
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            AssertReady(scene);
            Assert.That(started, Is.True, "A protected grid click must be consumed without starting a merge.");
            Assert.That(entities.EntityExists(scene.First), Is.True);
            Assert.That(entities.EntityExists(scene.Second), Is.True);
            var maps = entities.System<SharedMapSystem>();
            Assert.That(maps.GetTileRef(scene.First, entities.GetComponent<MapGridComponent>(scene.First), Vector2i.Zero).Tile.IsEmpty, Is.False);
            Assert.That(maps.GetTileRef(scene.Second, entities.GetComponent<MapGridComponent>(scene.Second), Vector2i.Right).Tile.IsEmpty, Is.False);
            Assert.That(entities.GetComponent<GridWeldingComponent>(scene.Welder).DoAfter, Is.Null);
            Assert.That(entities.System<SharedToolSystem>().GetWelderFuelAndCapacity(scene.Welder).fuel, Is.EqualTo(fuel));
        });
        await server.WaitPost(() => entities.DeleteEntity(scene.Map));
        await pair.CleanReturnAsync();
    }

    /// <summary>Preserves existing NavMap labels and indexes an anchored incoming beacon after an optional quarter turn.</summary>
    [TestCase(false)]
    [TestCase(true)]
    public async Task IncomingBeaconJoinsExistingNavMap(bool quarterTurn)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var entities = server.EntMan;
        WeldingScene scene = default;
        EntityUid existing = default;
        EntityUid incoming = default;
        Vector2 incomingWorld = default;
        var anchoredExisting = false;
        var anchoredIncoming = false;
        var started = false;

        await server.WaitPost(() =>
        {
            scene = CreateScene(pair, quarterTurn: quarterTurn);
            entities.System<SharedPhysicsSystem>().SetBodyType(scene.First, BodyType.Static);
            entities.EnsureComponent<NavMapComponent>(scene.First);
            entities.EnsureComponent<NavMapComponent>(scene.Second);
            existing = AddBeacon(entities, new EntityCoordinates(scene.First, 0.5f, 0.5f), "FishGridWeldingExistingBeacon");
            incoming = AddBeacon(entities, scene.SecondClick, "FishGridWeldingIncomingBeacon");
            anchoredExisting = entities.GetComponent<TransformComponent>(existing).Anchored;
            anchoredIncoming = entities.GetComponent<TransformComponent>(incoming).Anchored;
            incomingWorld = entities.System<SharedTransformSystem>().GetWorldPosition(incoming);
            started = StartWelding(entities, scene);
        });
        await server.WaitAssertion(() =>
        {
            AssertReady(scene);
            Assert.That(started, Is.True);
            Assert.That(anchoredExisting, Is.True);
            Assert.That(anchoredIncoming, Is.True);
        });
        await WaitForMerge(pair, scene.Second);
        await server.WaitAssertion(() =>
        {
            var transform = entities.System<SharedTransformSystem>();
            var xform = entities.GetComponent<TransformComponent>(incoming);
            Assert.That(xform.Anchored, Is.True);
            Assert.That(xform.GridUid, Is.EqualTo(scene.First));
            Assert.That(Vector2.Distance(transform.GetWorldPosition(incoming), incomingWorld), Is.LessThan(0.0001f));
            var beacons = entities.GetComponent<NavMapComponent>(scene.First).Beacons;
            Assert.That(beacons.Count, Is.EqualTo(2));
            Assert.That(beacons.ContainsKey(entities.GetNetEntity(existing)), Is.True);
            Assert.That(beacons[entities.GetNetEntity(existing)].Text, Is.EqualTo("Existing beacon"));
            Assert.That(beacons.ContainsKey(entities.GetNetEntity(incoming)), Is.True,
                "An anchored incoming beacon must be indexed even when the survivor already has a NavMap.");
            var beacon = beacons[entities.GetNetEntity(incoming)];
            Assert.That(beacon.Text, Is.EqualTo("Incoming beacon"));
            Assert.That(Vector2.Distance(beacon.Position, xform.LocalPosition), Is.LessThan(0.0001f));
        });
        await server.WaitPost(() => entities.DeleteEntity(scene.Map));
        await pair.CleanReturnAsync();
    }

    /// <summary>Checks total linear momentum using the actual fixture masses before and after two moving floor tiles merge.</summary>
    [Test]
    public async Task RebuiltFixturesConserveTotalLinearMomentum()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var entities = server.EntMan;
        WeldingScene scene = default;
        Vector2 momentum = default;
        var firstMass = 0f;
        var secondMass = 0f;
        var started = false;

        await server.WaitPost(() =>
        {
            scene = CreateScene(pair);
            var physics = entities.System<SharedPhysicsSystem>();
            physics.SetLinearVelocity(scene.First, new Vector2(2, 1));
            physics.SetLinearVelocity(scene.Second, new Vector2(2.125f, 1));
            var first = entities.GetComponent<PhysicsComponent>(scene.First);
            var second = entities.GetComponent<PhysicsComponent>(scene.Second);
            // Убираем внешнее демпфирование: проверяем только импульс, изменённый слиянием фикстур.
            physics.SetLinearDamping(scene.First, first, 0);
            physics.SetLinearDamping(scene.Second, second, 0);
            var friction = entities.System<TileFrictionController>();
            friction.SetModifier(scene.First, 0, entities.AddComponent<TileFrictionModifierComponent>(scene.First));
            friction.SetModifier(scene.Second, 0, entities.AddComponent<TileFrictionModifierComponent>(scene.Second));
            firstMass = first.Mass;
            secondMass = second.Mass;
            momentum = first.Momentum + second.Momentum;
            started = StartWelding(entities, scene);
        });
        await server.WaitAssertion(() =>
        {
            AssertReady(scene);
            Assert.That(started, Is.True);
            Assert.That(firstMass, Is.GreaterThan(0));
            Assert.That(secondMass, Is.GreaterThan(0));
        });
        await WaitForMerge(pair, scene.Second);
        await server.WaitAssertion(() =>
        {
            var merged = entities.GetComponent<PhysicsComponent>(scene.First);
            Assert.That(merged.BodyType, Is.EqualTo(BodyType.Dynamic));
            Assert.That(merged.Momentum.X, Is.EqualTo(momentum.X).Within(0.00001f));
            Assert.That(merged.Momentum.Y, Is.EqualTo(momentum.Y).Within(0.00001f));
        });
        await server.WaitPost(() => entities.DeleteEntity(scene.Map));
        await pair.CleanReturnAsync();
    }

    /// <summary>Transfers every decal from a connected sparse L-shaped donor, preserving placement and visual properties.</summary>
    [TestCase(false, 0)]
    [TestCase(false, 3)]
    [TestCase(true, 3)]
    public async Task SparseDonorPreservesAllDecals(bool quarterTurn, int count)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var entities = server.EntMan;
        WeldingScene scene = default;
        var added = 0;
        var started = false;
        var expected = new List<(Vector2 Position, Angle Rotation, Color Color, int ZIndex)>();

        await server.WaitPost(() =>
        {
            scene = CreateScene(pair, quarterTurn: quarterTurn);
            entities.System<SharedPhysicsSystem>().SetBodyType(scene.First, BodyType.Static);
            var tiles = new List<(Vector2i, Tile)>();
            var plating = Plating(pair);
            const int length = 256;
            for (var step = 1; step <= length; step++)
            {
                tiles.Add((quarterTurn ? new Vector2i(0, -step) : new Vector2i(1, step), plating));
                tiles.Add((quarterTurn ? new Vector2i(step, -length) : new Vector2i(step + 1, length), plating));
            }
            entities.System<SharedMapSystem>().SetTiles(scene.Second, entities.GetComponent<MapGridComponent>(scene.Second), tiles);
            var transform = entities.System<SharedTransformSystem>();
            var matrix = transform.GetWorldMatrix(scene.Second) * transform.GetInvWorldMatrix(scene.First);
            Vector2[] positions = quarterTurn
                ? [new(0.15f, 0.3f), new(0.2f, -length + 0.3f), new(length - 0.9f, -length + 0.4f)]
                : [new(1.15f, 0.3f), new(1.2f, length + 0.3f), new(length + 0.1f, length + 0.4f)];
            for (var index = 0; index < count; index++)
            {
                var angle = Angle.FromDegrees(15 + index * 20);
                var color = index == 0 ? Color.Red : index == 1 ? Color.Green : Color.Blue;
                if (entities.System<DecalSystem>().TryAddDecal("Dirt", new EntityCoordinates(scene.Second, positions[index]),
                        out _, color, angle, index, true))
                    added++;
                expected.Add((Vector2.Transform(positions[index] + new Vector2(0.5f), matrix) - new Vector2(0.5f),
                    angle + (quarterTurn ? Angle.FromDegrees(90) : Angle.Zero), color, index));
            }
            started = StartWelding(entities, scene);
        });
        await server.WaitAssertion(() =>
        {
            AssertReady(scene);
            Assert.That(added, Is.EqualTo(count));
            Assert.That(started, Is.True);
        });
        await WaitForMerge(pair, scene.Second);
        await server.WaitAssertion(() =>
        {
            var grid = entities.GetComponent<MapGridComponent>(scene.First);
            var decals = entities.System<DecalSystem>().GetDecalsIntersecting(scene.First, grid.LocalAABB.Enlarged(2));
            Assert.That(decals.Count, Is.EqualTo(count));
            foreach (var decal in expected)
            {
                var actual = decals.Single(entry => entry.Decal.ZIndex == decal.ZIndex).Decal;
                Assert.That(Vector2.Distance(actual.Coordinates, decal.Position), Is.LessThan(0.0001f));
                Assert.That(Math.Abs((actual.Angle - decal.Rotation).Reduced().Theta), Is.LessThan(0.0001));
                Assert.That(actual.Color, Is.EqualTo(decal.Color));
                Assert.That(actual.Cleanable, Is.True);
                Assert.That(actual.Id, Is.EqualTo("Dirt"));
            }
        });
        await server.WaitPost(() => entities.DeleteEntity(scene.Map));
        await pair.CleanReturnAsync();
    }

    /// <summary>Bounds synchronous merge work when independent instant actors finish welding during the same tick.</summary>
    [Test]
    public async Task SimultaneousInstantActionsMergeAtMostOnePairPerTick()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var entities = server.EntMan;
        WeldingScene first = default;
        WeldingScene second = default;
        var firstStarted = false;
        var secondStarted = false;
        var previous = 0;

        await server.WaitPost(() =>
        {
            first = CreateScene(pair);
            second = CreateScene(pair, new Vector2(10, 0), first.Map);
            firstStarted = StartWelding(entities, first);
            secondStarted = StartWelding(entities, second);
        });
        await server.WaitAssertion(() =>
        {
            AssertReady(first);
            AssertReady(second);
            Assert.That(firstStarted && secondStarted, Is.True);
            previous = CountMerged(entities, first, second);
            Assert.That(previous, Is.LessThanOrEqualTo(1), "Two actions in one server tick must not perform two atomic merges.");
        });
        for (var tick = 0; tick < 3; tick++)
        {
            await pair.RunTicksSync(1);
            await server.WaitAssertion(() =>
            {
                var merged = CountMerged(entities, first, second);
                Assert.That(merged - previous, Is.InRange(0, 1), "Only one grid pair may merge during a server tick.");
                previous = merged;
            });
        }
        await server.WaitAssertion(() =>
        {
            Assert.That(previous, Is.EqualTo(2));
            AssertSelectionCleared(entities, first.Welder);
            AssertSelectionCleared(entities, second.Welder);
        });
        await server.WaitPost(() => entities.DeleteEntity(first.Map));
        await pair.CleanReturnAsync();
    }

    /// <summary>Cancels queued welding when the tool is dropped without affecting a replacement action on the same seam.</summary>
    [TestCase(false)]
    [TestCase(true)]
    public async Task DroppingPendingWelderCancelsOnlyItsOwnAction(bool retry)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var entities = server.EntMan;
        WeldingScene scene = default;
        EntityUid third = default;
        FixedPoint2 fuel = default;
        var started = false;
        var dropped = false;
        var pickedUp = false;
        var restarted = false;

        await server.WaitPost(() =>
        {
            scene = CreateScene(pair);
            fuel = entities.System<SharedToolSystem>().GetWelderFuelAndCapacity(scene.Welder).fuel;
            started = StartWelding(entities, scene);
            var hands = entities.System<SharedHandsSystem>();
            dropped = hands.TryDrop(scene.User, scene.Welder);
            if (!retry)
                return;

            var maps = entities.System<SharedMapSystem>();
            var grid = maps.CreateGridEntity(entities.GetComponent<TransformComponent>(scene.First).MapID);
            third = grid.Owner;
            maps.SetTile(grid, new Vector2i(0, -1), Plating(pair));
            pickedUp = hands.TryPickupAnyHand(scene.User, scene.Welder);
            var welding = entities.System<GridWeldingSystem>();
            restarted = welding.TrySelectTile((scene.Welder, null), scene.User, scene.FirstClick) &&
                        welding.TrySelectTile((scene.Welder, null), scene.User, new EntityCoordinates(third, 0.5f, -0.5f));
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            AssertReady(scene);
            Assert.That(started, Is.True);
            Assert.That(dropped, Is.True);
            Assert.That(entities.EntityExists(scene.First), Is.True);
            Assert.That(entities.EntityExists(scene.Second), Is.True, "A cancelled queued request must never consume its old target.");
            if (retry)
            {
                Assert.That(pickedUp && restarted, Is.True);
                Assert.That(entities.EntityExists(third), Is.False, "The cancelled request must not cancel the new action using the same seam.");
            }
            Assert.That(entities.System<SharedToolSystem>().GetWelderFuelAndCapacity(scene.Welder).fuel,
                Is.EqualTo(fuel - FixedPoint2.New(retry ? 2 : 0)));
            AssertSelectionCleared(entities, scene.Welder);
        });
        await server.WaitPost(() => entities.DeleteEntity(scene.Map));
        await pair.CleanReturnAsync();
    }

    /// <summary>Keeps a paused map's pending action while allowing a ready action on another map to merge.</summary>
    [Test]
    public async Task PausedRequestDoesNotBlockReadyRequestOnAnotherMap()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var entities = server.EntMan;
        WeldingScene paused = default;
        WeldingScene ready = default;
        FixedPoint2 pausedFuel = default;
        var pausedStarted = false;
        var readyStarted = false;

        await server.WaitPost(() =>
        {
            paused = CreateScene(pair);
            ready = CreateScene(pair);
            pausedFuel = entities.System<SharedToolSystem>().GetWelderFuelAndCapacity(paused.Welder).fuel;
            pausedStarted = StartWelding(entities, paused);
            entities.System<SharedMapSystem>().SetPaused(paused.Map, true);
            readyStarted = StartWelding(entities, ready);
        });
        await server.WaitAssertion(() =>
        {
            AssertReady(paused);
            AssertReady(ready);
            Assert.That(pausedStarted && readyStarted, Is.True);
            Assert.That(entities.GetComponent<GridWeldingComponent>(paused.Welder).PendingWeld, Is.Not.Null);
            Assert.That(entities.GetComponent<GridWeldingComponent>(ready.Welder).PendingWeld, Is.Not.Null);
        });

        await WaitForMerge(pair, ready.Second);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.System<SharedMapSystem>().IsPaused(paused.Map), Is.True);
            Assert.That(entities.EntityExists(paused.First), Is.True);
            Assert.That(entities.EntityExists(paused.Second), Is.True);
            Assert.That(entities.GetComponent<GridWeldingComponent>(paused.Welder).PendingWeld, Is.Not.Null,
                "Pausing an input must retain its queued action rather than cancelling it.");
            Assert.That(entities.System<SharedToolSystem>().GetWelderFuelAndCapacity(paused.Welder).fuel, Is.EqualTo(pausedFuel));
            AssertSelectionCleared(entities, ready.Welder);
        });

        await server.WaitPost(() => entities.System<SharedMapSystem>().SetPaused(paused.Map, false));
        await WaitForMerge(pair, paused.Second);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(paused.First), Is.True);
            Assert.That(entities.System<SharedToolSystem>().GetWelderFuelAndCapacity(paused.Welder).fuel,
                Is.EqualTo(pausedFuel - FixedPoint2.New(2)));
            AssertSelectionCleared(entities, paused.Welder);
        });
        await server.WaitPost(() =>
        {
            entities.DeleteEntity(paused.Map);
            entities.DeleteEntity(ready.Map);
        });
        await pair.CleanReturnAsync();
    }

    /// <summary>Clears queued welding state on round cleanup before a real entity flush can mask a delayed merge.</summary>
    [Test]
    public async Task RoundCleanupClearsPendingWeldingWithoutAnEntityFlush()
    {
        // Событие сбрасывает состояние других систем, поэтому пару после проверки нельзя возвращать в общий пул.
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Fresh = true, Dirty = true, Destructive = true });
        var server = pair.Server;
        var entities = server.EntMan;
        WeldingScene scene = default;
        FixedPoint2 fuel = default;
        var started = false;

        await server.WaitPost(() =>
        {
            scene = CreateScene(pair);
            fuel = entities.System<SharedToolSystem>().GetWelderFuelAndCapacity(scene.Welder).fuel;
            started = StartWelding(entities, scene);
        });
        await server.WaitAssertion(() =>
        {
            AssertReady(scene);
            Assert.That(started, Is.True);
            Assert.That(entities.GetComponent<GridWeldingComponent>(scene.Welder).PendingWeld, Is.Not.Null);
        });

        await server.WaitPost(() => entities.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent()));
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(scene.First), Is.True);
            Assert.That(entities.EntityExists(scene.Second), Is.True);
            AssertSelectionCleared(entities, scene.Welder);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(scene.First), Is.True);
            Assert.That(entities.EntityExists(scene.Second), Is.True, "A round-cleaned queue entry must never execute later.");
            Assert.That(entities.System<SharedToolSystem>().GetWelderFuelAndCapacity(scene.Welder).fuel, Is.EqualTo(fuel));
            AssertSelectionCleared(entities, scene.Welder);
        });
        await server.WaitPost(() => entities.DeleteEntity(scene.Map));
        await pair.CleanReturnAsync();
    }

    /// <summary>Rechecks active magnet ownership when an input becomes tracked after the instant action has already queued.</summary>
    [TestCase(false)]
    [TestCase(true)]
    public async Task MagnetActivatedAfterWeldingStartsRejectsTheQueuedMerge(bool targetFirst)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var entities = server.EntMan;
        WeldingScene scene = default;
        FixedPoint2 fuel = default;
        var started = false;
        var queuedBeforeMagnet = false;

        await server.WaitPost(() =>
        {
            scene = CreateScene(pair);
            fuel = entities.System<SharedToolSystem>().GetWelderFuelAndCapacity(scene.Welder).fuel;
            started = StartWelding(entities, scene);
            queuedBeforeMagnet = entities.GetComponent<GridWeldingComponent>(scene.Welder).PendingWeld != null;
            var station = entities.SpawnEntity(null, new EntityCoordinates(scene.Map, -5, -5));
            var magnet = entities.AddComponent<SalvageMagnetDataComponent>(station);
            magnet.ActiveEntities = [targetFirst ? scene.First : scene.Second];
            magnet.EndTime = server.Timing.CurTime + TimeSpan.FromMinutes(5);
            magnet.NextOffer = server.Timing.CurTime + TimeSpan.FromMinutes(10);
        });
        await server.WaitAssertion(() =>
        {
            AssertReady(scene);
            Assert.That(started, Is.True);
            Assert.That(queuedBeforeMagnet, Is.True, "The magnet must be activated after initial welding validation.");
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(scene.First), Is.True);
            Assert.That(entities.EntityExists(scene.Second), Is.True);
            var state = entities.GetComponent<GridWeldingComponent>(scene.Welder);
            Assert.That(state.DoAfter, Is.Null);
            Assert.That(state.PendingWeld, Is.Null);
            Assert.That(entities.System<SharedToolSystem>().GetWelderFuelAndCapacity(scene.Welder).fuel, Is.EqualTo(fuel));
        });
        await server.WaitPost(() => entities.DeleteEntity(scene.Map));
        await pair.CleanReturnAsync();
    }

    /// <summary>Rechecks interaction permission when a queued actor becomes stunned while still holding the welder.</summary>
    [Test]
    public async Task StunnedUserCannotFinishQueuedWelding()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var entities = server.EntMan;
        WeldingScene scene = default;
        FixedPoint2 fuel = default;
        var started = false;
        var queuedBeforeStun = false;

        await server.WaitPost(() =>
        {
            scene = CreateScene(pair);
            fuel = entities.System<SharedToolSystem>().GetWelderFuelAndCapacity(scene.Welder).fuel;
            started = StartWelding(entities, scene);
            queuedBeforeStun = entities.GetComponent<GridWeldingComponent>(scene.Welder).PendingWeld != null;
            // Маркер блокирует взаимодействие, сохраняя инструмент в руке для проверки именно прав завершения.
            entities.EnsureComponent<StunnedComponent>(scene.User);
        });
        await server.WaitAssertion(() =>
        {
            AssertReady(scene);
            Assert.That(started, Is.True);
            Assert.That(queuedBeforeStun, Is.True);
            Assert.That(entities.System<SharedHandsSystem>().GetActiveItem(scene.User), Is.EqualTo(scene.Welder));
            Assert.That(entities.System<ActionBlockerSystem>().CanInteract(scene.User, null), Is.False,
                "The queued actor must actually lose interaction permission before completion is processed.");
        });

        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(scene.First), Is.True);
            Assert.That(entities.EntityExists(scene.Second), Is.True, "A stunned actor must not finish a previously queued weld.");
            var state = entities.GetComponent<GridWeldingComponent>(scene.Welder);
            Assert.That(state.DoAfter, Is.Null);
            Assert.That(state.PendingWeld, Is.Null);
            Assert.That(entities.System<SharedToolSystem>().GetWelderFuelAndCapacity(scene.Welder).fuel, Is.EqualTo(fuel));
        });
        await server.WaitPost(() => entities.DeleteEntity(scene.Map));
        await pair.CleanReturnAsync();
    }

    private static WeldingScene CreateScene(TestPair pair, Vector2 origin = default, EntityUid? existingMap = null,
        bool quarterTurn = false)
    {
        var entities = pair.Server.EntMan;
        var maps = entities.System<SharedMapSystem>();
        var transform = entities.System<SharedTransformSystem>();
        var map = existingMap ?? maps.CreateMap(out _);
        var mapId = entities.GetComponent<TransformComponent>(map).MapID;
        var first = maps.CreateGridEntity(mapId);
        var second = maps.CreateGridEntity(mapId);
        maps.SetTile(first, Vector2i.Zero, Plating(pair));
        maps.SetTile(second, quarterTurn ? Vector2i.Zero : Vector2i.Right, Plating(pair));
        transform.SetWorldPosition(first.Owner, origin);
        transform.SetWorldPositionRotation(second.Owner, origin + (quarterTurn ? new Vector2(2, 0) : Vector2.Zero),
            quarterTurn ? Angle.FromDegrees(90) : Angle.Zero);
        var physics = entities.System<SharedPhysicsSystem>();
        physics.SetBodyType(first.Owner, BodyType.Dynamic);
        physics.SetBodyType(second.Owner, BodyType.Dynamic);

        // Пользователь остаётся под гридом, чтобы его тело и инструмент не влияли на массу напольных фикстур.
        var coordinates = new EntityCoordinates(map, origin + new Vector2(0.5f, -0.5f));
        var user = entities.SpawnEntity("AdminObserver", coordinates);
        var welder = entities.SpawnEntity("FishGridWeldingTestWelder", coordinates);
        var pickedUp = entities.System<SharedHandsSystem>().TryPickupAnyHand(user, welder);
        var activated = entities.System<ItemToggleSystem>().TryActivate(welder, user);
        return new WeldingScene(map, first.Owner, second.Owner, user, welder,
            new EntityCoordinates(first, 0.5f, 0.5f),
            new EntityCoordinates(second, quarterTurn ? new Vector2(0.5f) : new Vector2(1.5f, 0.5f)), pickedUp, activated);
    }

    private static Tile Plating(TestPair pair)
    {
        return new Tile(pair.Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
    }

    private static bool StartWelding(IEntityManager entities, WeldingScene scene)
    {
        var welding = entities.System<GridWeldingSystem>();
        return welding.TrySelectTile((scene.Welder, null), scene.User, scene.FirstClick) &&
               welding.TrySelectTile((scene.Welder, null), scene.User, scene.SecondClick);
    }

    private static EntityUid AddBeacon(IEntityManager entities, EntityCoordinates coordinates, string prototype)
    {
        var beacon = entities.SpawnEntity(prototype, coordinates);
        entities.System<SharedTransformSystem>().AnchorEntity(beacon);
        return beacon;
    }

    private static void AssertReady(WeldingScene scene)
    {
        Assert.That(scene.PickedUp, Is.True);
        Assert.That(scene.Activated, Is.True);
    }

    private static void AssertSelectionCleared(IEntityManager entities, EntityUid welder)
    {
        var state = entities.GetComponent<GridWeldingComponent>(welder);
        Assert.That(state.DoAfter, Is.Null, "An instant DoAfter must not leave a stale ID on the welder.");
        Assert.That(state.PendingWeld, Is.Null, "A finished or cancelled action must release its queued completion.");
        var user = state.User;
        var grid = state.Grid;
        Assert.That(user.IsValid(), Is.False);
        Assert.That(grid.IsValid(), Is.False);
    }

    private static int CountMerged(IEntityManager entities, WeldingScene first, WeldingScene second)
    {
        return (entities.EntityExists(first.Second) ? 0 : 1) + (entities.EntityExists(second.Second) ? 0 : 1);
    }

    /// <summary>Allows the deferred completion path to run without trusting a potentially stale instant DoAfter ID.</summary>
    private static async Task WaitForMerge(TestPair pair, EntityUid donor)
    {
        var merged = false;
        for (var tick = 0; tick < 3; tick++)
        {
            await pair.RunTicksSync(1);
            await pair.Server.WaitAssertion(() => merged = !pair.Server.EntMan.EntityExists(donor));
            if (merged)
                return;
        }
        Assert.That(merged, Is.True, "The instant welding action must merge its donor within the completion allowance.");
    }

    private readonly record struct WeldingScene(EntityUid Map, EntityUid First, EntityUid Second, EntityUid User,
        EntityUid Welder, EntityCoordinates FirstClick, EntityCoordinates SecondClick, bool PickedUp, bool Activated);
}
