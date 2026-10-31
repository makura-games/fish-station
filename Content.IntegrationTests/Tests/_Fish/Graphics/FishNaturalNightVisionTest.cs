using System.Linq;
using Content.Client._Fish.Graphics.Vision;
using Content.Client.DoAfter;
using Content.Client.Light;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Overlays;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using ServerNightVisionSystem = Content.Server.NightVision.NightVisionSystem;

namespace Content.IntegrationTests.Tests._Fish.Graphics;

[TestFixture]
public sealed class FishNaturalNightVisionTest
{
    private const int NetworkTicks = 20;

    [TestCase("MobFelinid")]
    [TestCase("MobTajaran")]
    [TestCase("MobResomi")]
    [TestCase("MobArachnid")]
    [TestCase("MobSkeletonPerson")]
    [TestCase("MobKitsunePlayerFox")]
    public async Task RacialVisionStartsOffAndUsesNaturalRenderingWhenToggled(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var session = server.PlayerMan.GetSessionById(client.Session!.UserId);
        var previousEntity = session.AttachedEntity;
        EntityUid wearer = default;
        await server.WaitPost(() =>
        {
            wearer = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            server.PlayerMan.SetAttachedEntity(session, wearer);
        });
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.TryGetComponent<NightVisionComponent>(wearer, out var vision), Is.True,
                "The racial ability must exist at spawn, including Tajaran eyes.");
            Assert.That(vision!.Enabled, Is.False);
            Assert.That(vision.ActionEntity, Is.Not.Null, "The existing action must toggle the racial ability.");
        });
        await pair.RunTicksSync(NetworkTicks);
        await Check(false);
        await server.WaitPost(() => server.System<ServerNightVisionSystem>().SetEnabled(wearer, true));
        await pair.RunTicksSync(NetworkTicks);
        await Check(true);
        await server.WaitPost(() =>
        {
            server.PlayerMan.SetAttachedEntity(session, previousEntity);
            server.EntMan.DeleteEntity(map.MapUid);
        });
        await pair.CleanReturnAsync();

        async Task Check(bool active) => await client.WaitAssertion(() =>
        {
            var overlays = client.ResolveDependency<IOverlayManager>();
            var natural = overlays.AllOverlays.SingleOrDefault(o => o.GetType().Name == "FishNaturalNightVisionOverlay");
            Assert.That(natural != null, Is.EqualTo(active),
                "Racial vision must use its natural rendering rather than a device effect.");
            if (natural == null)
                return;

            Assert.That(natural.ZIndex ?? 0, Is.GreaterThan(AfterLightTargetOverlay.ContentZIndex),
                "Ambient composition must finish before natural vision adds light to its target.");
            Assert.That(natural.ZIndex ?? 0, Is.LessThan(overlays.GetOverlay<DoAfterOverlay>().ZIndex ?? 0),
                "Natural vision must run before action indicators are rendered.");
        });
    }

    [Test]
    public async Task EquipmentTemporarilyOverridesNaturalVisionAndDetachingClearsIt()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var session = server.PlayerMan.GetSessionById(client.Session!.UserId);
        var previousEntity = session.AttachedEntity;
        EntityUid wearer = default;
        var equipped = false;
        var activated = false;
        await server.WaitPost(() =>
        {
            wearer = server.EntMan.SpawnEntity("MobFelinid", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(session, wearer);
            server.System<ServerNightVisionSystem>().SetEnabled(wearer, true);
        });
        await Check(true, false);
        await server.WaitPost(() =>
        {
            var goggles = server.EntMan.SpawnEntity("ClothingEyesNVD", map.GridCoords);
            equipped = server.System<InventorySystem>().TryEquip(wearer, goggles, "eyes", force: true);
            activated = server.System<ItemToggleSystem>().TryActivate(goggles, wearer);
        });
        Assert.That(equipped, Is.True);
        Assert.That(activated, Is.True);
        await Check(false, true);
        await server.WaitPost(() => server.System<InventorySystem>().TryUnequip(wearer, "eyes", force: true));
        await Check(true, false);
        await server.WaitPost(() => server.System<ServerNightVisionSystem>().SetEnabled(wearer, false));
        await Check(false, false);
        await server.WaitPost(() => server.System<ServerNightVisionSystem>().SetEnabled(wearer, true));
        await Check(true, false);
        await server.WaitPost(() => server.PlayerMan.SetAttachedEntity(session,
            server.EntMan.SpawnEntity("MobHuman", map.GridCoords)));
        await Check(false, false);
        await server.WaitPost(() =>
        {
            server.PlayerMan.SetAttachedEntity(session, previousEntity);
            server.EntMan.DeleteEntity(map.MapUid);
        });
        await pair.CleanReturnAsync();

        async Task Check(bool natural, bool device)
        {
            await pair.RunTicksSync(NetworkTicks);
            await client.WaitAssertion(() =>
            {
                Assert.That(client.ResolveDependency<IOverlayManager>().AllOverlays.Any(o => o.GetType().Name == "FishNaturalNightVisionOverlay"),
                    Is.EqualTo(natural));
                Assert.That(client.System<FishVisionSystem>().NightPresentation.Source != null, Is.EqualTo(device));
            });
        }
    }

    [TestCase("MobHuman")]
    [TestCase("MacKitsune")]
    public async Task DaylightBodiesDoNotGainAnInnateNightVisionAbility(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid wearer = default;
        await server.WaitPost(() => wearer = server.EntMan.SpawnEntity(prototype, map.GridCoords));
        await server.WaitAssertion(() => Assert.That(server.EntMan.HasComponent<NightVisionComponent>(wearer), Is.False));
        await server.WaitPost(() => server.EntMan.DeleteEntity(map.MapUid));
        await pair.CleanReturnAsync();
    }
}
