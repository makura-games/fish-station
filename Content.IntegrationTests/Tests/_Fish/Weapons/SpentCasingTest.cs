using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared._Fish.Weapons.Casings;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Spawners;

namespace Content.IntegrationTests.Tests._Fish.Weapons;

[TestFixture]
public sealed class SpentCasingTest
{
    private const float DespawnSeconds = 60f;

    [Test]
    public async Task LooseRounds_OnlySpentCasingsDespawnAndRejectPickup()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var user = entMan.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(0f, 6f)));
            var gun = entMan.SpawnEntity("WeaponPistolMk58", map.GridCoords);
            var gunComp = entMan.GetComponent<GunComponent>(gun);
            var hands = entMan.System<SharedHandsSystem>();
            var guns = entMan.System<SharedGunSystem>();

            AssertLiveRound(entMan, hands, user, "CartridgePistolFMJ");
            AssertSpentAfterShot(entMan, guns, hands, user, (gun, gunComp), "CartridgePistolFMJ");
            AssertSpentAfterShot(entMan, guns, hands, user, (gun, gunComp), "CartridgeRifleFMJ");
            AssertSpentAfterShot(entMan, guns, hands, user, (gun, gunComp), "ShellShotgun");

            var alreadySpent = entMan.SpawnEntity("CartridgePistolSpent", map.GridCoords);
            AssertSpentCasing(entMan, hands, user, alreadySpent, DespawnSeconds);

            var caseless = entMan.SpawnEntity("CartridgeCaselessRifle", map.GridCoords);
            Shoot(entMan, guns, (gun, gunComp), caseless);
            Assert.That(entMan.Deleted(caseless), Is.True, "Безгильзовый патрон должен исчезнуть сразу.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Revolver_SpentCasingCountsFromShot_LiveEjectStays()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid user = default;
        EntityUid revolver = default;
        EntityUid spent = default;

        await server.WaitPost(() =>
        {
            user = entMan.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(0f, 6f)));
            revolver = entMan.SpawnEntity("WeaponRevolverInspector", map.GridCoords);
            var gunComp = entMan.GetComponent<GunComponent>(revolver);
            var coords = entMan.GetComponent<TransformComponent>(revolver).Coordinates;
            var taken = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), coords, user);
            entMan.EventBus.RaiseLocalEvent(revolver, taken);
            Assert.That(taken.Ammo, Is.Not.Empty, "Револьвер не отдал патрон.");
            entMan.System<SharedGunSystem>().Shoot((revolver, gunComp), taken.Ammo, coords, coords.Offset(new Vector2(1f, 0f)), out _);

            spent = entMan.GetComponent<RevolverAmmoProviderComponent>(revolver).AmmoSlots.Single(slot => slot != null)!.Value;
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<CartridgeAmmoComponent>(spent).Spent, Is.True);
            Assert.That(entMan.HasComponent<SpentCasingComponent>(spent), Is.True);
            Assert.That(entMan.HasComponent<TimedDespawnComponent>(spent), Is.False,
                "Гильза в барабане не должна исчезнуть, пока её не выбросили.");
            Assert.That(entMan.System<SharedHandsSystem>().CanPickupAnyHand(user, spent), Is.False);
        });

        await pair.RunSeconds(1f);

        HashSet<EntityUid> before = default!;
        await server.WaitPost(() =>
        {
            before = CollectCartridges(entMan);
            entMan.System<SharedGunSystem>().EmptyRevolver((revolver, entMan.GetComponent<RevolverAmmoProviderComponent>(revolver)), user);
        });

        await server.WaitAssertion(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();
            AssertSpentCasing(entMan, hands, user, spent, DespawnSeconds - 1f);

            var live = CollectCartridges(entMan).Where(uid => !before.Contains(uid)).ToList();
            Assert.That(live, Is.Not.Empty, "Досыл револьвера должен выбросить целые патроны.");
            foreach (var uid in live)
            {
                Assert.That(entMan.GetComponent<CartridgeAmmoComponent>(uid).Spent, Is.False);
                Assert.That(entMan.HasComponent<TimedDespawnComponent>(uid), Is.False);
                Assert.That(hands.CanPickupAnyHand(user, uid), Is.True);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Pump_SpentShellDespawnsAfterCycle_LiveShellStays()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid user = default;
        EntityUid shotgun = default;
        EntityUid spent = default;

        await server.WaitPost(() =>
        {
            user = entMan.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(0f, 6f)));
            shotgun = entMan.SpawnEntity("WeaponShotgunRiotSawn", map.GridCoords);
            var provider = entMan.GetComponent<BallisticAmmoProviderComponent>(shotgun);
            Assert.That(provider.UnspawnedCount, Is.GreaterThan(0));

            var coords = entMan.GetComponent<TransformComponent>(shotgun).Coordinates;
            var taken = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), coords, user);
            entMan.EventBus.RaiseLocalEvent(shotgun, taken);
            Assert.That(taken.Ammo, Is.Not.Empty, "Помповое ружьё не отдало патрон.");
            var gunComp = entMan.GetComponent<GunComponent>(shotgun);
            entMan.System<SharedGunSystem>().Shoot((shotgun, gunComp), taken.Ammo, coords, coords.Offset(new Vector2(1f, 0f)), out _);
            spent = entMan.GetComponent<BallisticAmmoProviderComponent>(shotgun).Entities[^1];
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<CartridgeAmmoComponent>(spent).Spent, Is.True);
            Assert.That(entMan.HasComponent<TimedDespawnComponent>(spent), Is.False,
                "Гильза в помпе не должна исчезнуть до передёргивания.");
        });

        await server.WaitPost(() =>
        {
            var use = new UseInHandEvent(user);
            entMan.EventBus.RaiseLocalEvent(shotgun, use);
        });

        await server.WaitAssertion(() =>
        {
            AssertSpentCasing(entMan, entMan.System<SharedHandsSystem>(), user, spent, DespawnSeconds);
        });

        EntityUid live = default;
        await server.WaitPost(() =>
        {
            var before = CollectCartridges(entMan);
            var use = new UseInHandEvent(user);
            entMan.EventBus.RaiseLocalEvent(shotgun, use);
            live = CollectCartridges(entMan).Single(uid => !before.Contains(uid));
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<CartridgeAmmoComponent>(live).Spent, Is.False);
            Assert.That(entMan.HasComponent<TimedDespawnComponent>(live), Is.False);
            Assert.That(entMan.System<SharedHandsSystem>().CanPickupAnyHand(user, live), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertLiveRound(IEntityManager entMan, SharedHandsSystem hands, EntityUid user, string prototype)
    {
        var round = entMan.SpawnEntity(prototype, entMan.GetComponent<TransformComponent>(user).Coordinates);
        Assert.That(entMan.GetComponent<CartridgeAmmoComponent>(round).Spent, Is.False);
        Assert.That(entMan.HasComponent<SpentCasingComponent>(round), Is.False);
        Assert.That(entMan.HasComponent<TimedDespawnComponent>(round), Is.False);
        Assert.That(hands.CanPickupAnyHand(user, round), Is.True);
    }

    private static void AssertSpentAfterShot(
        IEntityManager entMan,
        SharedGunSystem guns,
        SharedHandsSystem hands,
        EntityUid user,
        Entity<GunComponent> gun,
        string prototype)
    {
        var round = entMan.SpawnEntity(prototype, entMan.GetComponent<TransformComponent>(gun).Coordinates);
        Shoot(entMan, guns, gun, round);
        Assert.That(entMan.GetComponent<CartridgeAmmoComponent>(round).Spent, Is.True, prototype);
        AssertSpentCasing(entMan, hands, user, round, DespawnSeconds);
    }

    private static void Shoot(IEntityManager entMan, SharedGunSystem guns, Entity<GunComponent> gun, EntityUid ammo)
    {
        var coordinates = entMan.GetComponent<TransformComponent>(gun).Coordinates;
        guns.Shoot(gun, ammo, coordinates, coordinates.Offset(new Vector2(1f, 0f)), out _);
    }

    private static void AssertSpentCasing(
        IEntityManager entMan,
        SharedHandsSystem hands,
        EntityUid user,
        EntityUid casing,
        float expectedLifetime)
    {
        Assert.That(entMan.HasComponent<SpentCasingComponent>(casing), Is.True);
        Assert.That(entMan.HasComponent<TimedDespawnComponent>(casing), Is.True);
        Assert.That(entMan.GetComponent<TimedDespawnComponent>(casing).Lifetime, Is.EqualTo(expectedLifetime).Within(1.5f));
        Assert.That(entMan.GetComponent<SpentCasingComponent>(casing).DespawnSeconds, Is.EqualTo(DespawnSeconds));
        Assert.That(hands.CanPickupAnyHand(user, casing), Is.False);
        Assert.That(hands.TryPickupAnyHand(user, casing), Is.False);
    }

    private static HashSet<EntityUid> CollectCartridges(IEntityManager entMan)
    {
        var found = new HashSet<EntityUid>();
        var query = entMan.EntityQueryEnumerator<CartridgeAmmoComponent>();
        while (query.MoveNext(out var uid, out _))
            found.Add(uid);

        return found;
    }
}
