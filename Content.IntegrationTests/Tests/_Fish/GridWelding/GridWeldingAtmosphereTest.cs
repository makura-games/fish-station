using System.Numerics;
using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Decals;
using Content.Server.Atmos.Monitor.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.GridWelding;

[TestFixture]
public sealed class GridWeldingAtmosphereTest
{
    /// <summary>
    /// Verifies that merging preserves tile state and reconnects moved sensors to the restored gas mixture.
    /// </summary>
    [Test]
    public async Task WeldingPreservesGasFireDecalsAndFloorLayers()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var entities = server.EntMan;
        EntityUid map = default;
        EntityUid first = default;
        EntityUid second = default;
        EntityUid sensor = default;
        EntityUid welder = default;

        await server.WaitPost(() =>
        {
            var maps = entities.System<SharedMapSystem>();
            map = maps.CreateMap(out var mapId);
            var a = maps.CreateGridEntity(mapId);
            var b = maps.CreateGridEntity(mapId);
            first = a.Owner;
            second = b.Owner;
            var tile = new Tile(server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            maps.SetTiles(a, [(new Vector2i(0, 0), tile), (new Vector2i(0, 1), tile)]);
            maps.SetTiles(b, [(new Vector2i(1, 0), tile), (new Vector2i(1, 1), tile)]);

            entities.EnsureComponent<GridAtmosphereComponent>(first);
            entities.EnsureComponent<GridAtmosphereComponent>(second);
        });

        await pair.RunSeconds(1);
        await server.WaitPost(() =>
        {
            var atmos = entities.System<AtmosphereSystem>();
            foreach (var grid in new[] { first, second })
            {
                var atmosphere = entities.GetComponent<GridAtmosphereComponent>(grid);
                // Изолируем перенос от утечки через открытые края тестовых гридов.
                atmos.SetAtmosphereSimulation((grid, atmosphere), false);
            }
            var gas = atmos.GetTileMixture(second, map, new Vector2i(1, 1));
            Assert.That(gas, Is.Not.Null);
            gas.AdjustMoles(Gas.Oxygen, 10);
            gas.AdjustMoles(Gas.Plasma, 2);
            gas.Temperature = 333;
            atmos.HotspotExpose((second, null), new Vector2i(1, 1), 1000, 20);
            Assert.That(atmos.IsHotspotActive(second, new Vector2i(1, 1)), Is.True);

            sensor = entities.SpawnEntity(null, new EntityCoordinates(second, 1.5f, 1.5f));
            entities.System<SharedTransformSystem>().AnchorEntity(sensor);
            entities.AddComponent<AtmosMonitorComponent>(sensor);
            entities.AddComponent<AtmosDeviceComponent>(sensor);
            Assert.That(entities.GetComponent<AtmosMonitorComponent>(sensor).TileGas, Is.SameAs(gas));

            var history = entities.GetComponent<TileHistoryComponent>(second);
            var chunk = new TileHistoryChunk();
            chunk.History[new Vector2i(1, 1)] = [new ProtoId<ContentTileDefinition>("Plating")];
            history.ChunkHistory[Vector2i.Zero] = chunk;
            Assert.That(entities.System<DecalSystem>().TryAddDecal("Dirt", new EntityCoordinates(second, 1, 1), out _));

            var user = entities.SpawnEntity("MobHuman", new EntityCoordinates(first, 0.5f, 0.5f));
            welder = entities.SpawnEntity("Welder", new EntityCoordinates(first, 0.5f, 0.5f));
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, welder));
            Assert.That(entities.System<ItemToggleSystem>().TryActivate(welder, user));
            entities.EventBus.RaiseLocalEvent(welder,
                new BeforeRangedInteractEvent(user, welder, null, new EntityCoordinates(first, 0.5f, 0.5f), true));
            entities.EventBus.RaiseLocalEvent(welder,
                new BeforeRangedInteractEvent(user, welder, null, new EntityCoordinates(second, 1.5f, 0.5f), true));
        });

        await GridWeldingTestHelpers.WaitForWeldingAsync(pair, welder);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(second), Is.False);
            var atmos = entities.System<AtmosphereSystem>();
            var gas = atmos.GetTileMixture(first, map, new Vector2i(1, 1));
            Assert.That(gas, Is.Not.Null);
            Assert.That(gas.GetMoles(Gas.Oxygen), Is.EqualTo(10).Within(0.001));
            Assert.That(gas.GetMoles(Gas.Plasma), Is.EqualTo(2).Within(0.001));
            Assert.That(gas.Temperature, Is.EqualTo(333).Within(0.001));
            Assert.That(entities.GetComponent<AtmosMonitorComponent>(sensor).TileGas, Is.SameAs(gas),
                "Moved monitors must observe the restored tile mixture.");
            Assert.That(atmos.IsHotspotActive(first, new Vector2i(1, 1)), Is.True);
            Assert.That(entities.System<DecalSystem>().GetDecalsIntersecting(first, new Box2(1, 1, 2, 2))
                .Any(d => d.Decal.Id == "Dirt"), Is.True);
            Assert.That(entities.GetComponent<TileHistoryComponent>(first).ChunkHistory[Vector2i.Zero]
                .History[new Vector2i(1, 1)].Single().Id, Is.EqualTo("Plating"));
        });
        await server.WaitPost(() => entities.DeleteEntity(map));
        await pair.CleanReturnAsync();
    }
}
