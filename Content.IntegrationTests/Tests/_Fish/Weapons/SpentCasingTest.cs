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
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Fish.Weapons;

[TestFixture]
public sealed class SpentCasingTest
{
    private const float DespawnSeconds = 60f;

    /// <summary>
    /// Несколько тиков при 30 Гц. Меньше секунды ожидания, поэтому сброс таймера на полные 60 с не проходит.
    /// </summary>
    private const float LifetimeTickTolerance = 0.15f;

    [Test]
    public async Task LooseRounds_OnlySpentCasingsDespawnAndRejectPickup()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();
        EntityUid user = default;
        EntityUid live = default;
        EntityUid pistol = default;
        EntityUid rifle = default;
        EntityUid shell = default;
        EntityUid alreadySpent = default;
        EntityUid caseless = default;
        var pickedUp = false;
        var pistolPickup = true;
        var riflePickup = true;
        var shellPickup = true;
        var spentPickup = true;

        await server.WaitPost(() =>
        {
            user = entMan.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(0f, 6f)));
            var gun = entMan.SpawnEntity("WeaponPistolMk58", map.GridCoords);
            var gunComp = entMan.GetComponent<GunComponent>(gun);
            var guns = entMan.System<SharedGunSystem>();
            var userCoords = entMan.GetComponent<TransformComponent>(user).Coordinates;

            live = entMan.SpawnEntity("CartridgePistolFMJ", userCoords);
            pistol = ShootRound(entMan, guns, (gun, gunComp), "CartridgePistolFMJ");
            rifle = ShootRound(entMan, guns, (gun, gunComp), "CartridgeRifleFMJ");
            shell = ShootRound(entMan, guns, (gun, gunComp), "ShellShotgun");
            alreadySpent = entMan.SpawnEntity("CartridgePistolSpent", map.GridCoords);
            caseless = entMan.SpawnEntity("CartridgeCaselessRifle", map.GridCoords);
            Shoot(entMan, guns, (gun, gunComp), caseless);
        });

        await server.WaitAssertion(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();
            AssertLiveRound(entMan, hands, user, live);
            AssertSpentCasing(entMan, timing, hands, user, pistol);
            AssertSpentCasing(entMan, timing, hands, user, rifle);
            AssertSpentCasing(entMan, timing, hands, user, shell);
            AssertSpentCasing(entMan, timing, hands, user, alreadySpent);
            Assert.That(entMan.Deleted(caseless), Is.True, "Безгильзовый патрон должен исчезнуть сразу.");
        });

        await server.WaitPost(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();
            pickedUp = hands.TryPickupAnyHand(user, live);
            pistolPickup = hands.TryPickupAnyHand(user, pistol);
            riflePickup = hands.TryPickupAnyHand(user, rifle);
            shellPickup = hands.TryPickupAnyHand(user, shell);
            spentPickup = hands.TryPickupAnyHand(user, alreadySpent);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(pickedUp, Is.True);
            Assert.That(pistolPickup, Is.False);
            Assert.That(riflePickup, Is.False);
            Assert.That(shellPickup, Is.False);
            Assert.That(spentPickup, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Revolver_SpentCasingCountsFromShot_LiveEjectStays()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();
        EntityUid user = default;
        EntityUid revolver = default;
        EntityUid spent = default;
        var ammoTaken = false;

        await server.WaitPost(() =>
        {
            user = entMan.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(0f, 6f)));
            revolver = entMan.SpawnEntity("WeaponRevolverInspector", map.GridCoords);
            var gunComp = entMan.GetComponent<GunComponent>(revolver);
            var coords = entMan.GetComponent<TransformComponent>(revolver).Coordinates;
            var taken = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), coords, user);
            entMan.EventBus.RaiseLocalEvent(revolver, taken);
            ammoTaken = taken.Ammo.Count > 0;
            entMan.System<SharedGunSystem>().Shoot((revolver, gunComp), taken.Ammo, coords, coords.Offset(new Vector2(1f, 0f)), out _);
            spent = entMan.GetComponent<RevolverAmmoProviderComponent>(revolver).AmmoSlots.Single(slot => slot != null)!.Value;
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(ammoTaken, Is.True, "Револьвер не отдал патрон.");
            Assert.That(entMan.GetComponent<CartridgeAmmoComponent>(spent).Spent, Is.True);
            Assert.That(entMan.HasComponent<SpentCasingComponent>(spent), Is.True);
            Assert.That(entMan.HasComponent<TimedDespawnComponent>(spent), Is.False,
                "Гильза в барабане не должна исчезнуть, пока её не выбросили.");
            Assert.That(entMan.System<SharedHandsSystem>().CanPickupAnyHand(user, spent), Is.False);
        });

        await pair.RunSeconds(1f);

        HashSet<EntityUid> before = default!;
        var spentPickup = true;
        await server.WaitPost(() =>
        {
            before = CollectCartridges(entMan);
            entMan.System<SharedGunSystem>().EmptyRevolver((revolver, entMan.GetComponent<RevolverAmmoProviderComponent>(revolver)), user);
            spentPickup = entMan.System<SharedHandsSystem>().TryPickupAnyHand(user, spent);
        });

        await server.WaitAssertion(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();
            AssertSpentCasing(entMan, timing, hands, user, spent);
            var age = CasingAge(entMan, timing, spent);
            Assert.That(age, Is.GreaterThan(LifetimeTickTolerance),
                "После секунды ожидания возраст гильзы должен быть больше допуска, иначе сброс на 60 с не отличить.");
            Assert.That(spentPickup, Is.False);

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
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();
        EntityUid user = default;
        EntityUid shotgun = default;
        EntityUid spent = default;
        var loaded = false;
        var ammoTaken = false;
        var spentPickup = true;

        await server.WaitPost(() =>
        {
            user = entMan.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(0f, 6f)));
            shotgun = entMan.SpawnEntity("WeaponShotgunRiotSawn", map.GridCoords);
            loaded = entMan.GetComponent<BallisticAmmoProviderComponent>(shotgun).UnspawnedCount > 0;

            var coords = entMan.GetComponent<TransformComponent>(shotgun).Coordinates;
            var taken = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), coords, user);
            entMan.EventBus.RaiseLocalEvent(shotgun, taken);
            ammoTaken = taken.Ammo.Count > 0;
            var gunComp = entMan.GetComponent<GunComponent>(shotgun);
            entMan.System<SharedGunSystem>().Shoot((shotgun, gunComp), taken.Ammo, coords, coords.Offset(new Vector2(1f, 0f)), out _);
            spent = entMan.GetComponent<BallisticAmmoProviderComponent>(shotgun).Entities[^1];
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(loaded, Is.True);
            Assert.That(ammoTaken, Is.True, "Помповое ружьё не отдало патрон.");
            Assert.That(entMan.GetComponent<CartridgeAmmoComponent>(spent).Spent, Is.True);
            Assert.That(entMan.HasComponent<TimedDespawnComponent>(spent), Is.False,
                "Гильза в помпе не должна исчезнуть до передёргивания.");
        });

        await server.WaitPost(() =>
        {
            var use = new UseInHandEvent(user);
            entMan.EventBus.RaiseLocalEvent(shotgun, use);
            spentPickup = entMan.System<SharedHandsSystem>().TryPickupAnyHand(user, spent);
        });

        await server.WaitAssertion(() =>
        {
            AssertSpentCasing(entMan, timing, entMan.System<SharedHandsSystem>(), user, spent);
            Assert.That(spentPickup, Is.False);
        });

        EntityUid live = default;
        var foundLive = false;
        await server.WaitPost(() =>
        {
            var before = CollectCartridges(entMan);
            var use = new UseInHandEvent(user);
            entMan.EventBus.RaiseLocalEvent(shotgun, use);
            var ejected = CollectCartridges(entMan).Where(uid => !before.Contains(uid)).ToList();
            foundLive = ejected.Count == 1;
            if (foundLive)
                live = ejected[0];
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(foundLive, Is.True);
            Assert.That(entMan.GetComponent<CartridgeAmmoComponent>(live).Spent, Is.False);
            Assert.That(entMan.HasComponent<TimedDespawnComponent>(live), Is.False);
            Assert.That(entMan.System<SharedHandsSystem>().CanPickupAnyHand(user, live), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertLiveRound(IEntityManager entMan, SharedHandsSystem hands, EntityUid user, EntityUid round)
    {
        Assert.That(entMan.GetComponent<CartridgeAmmoComponent>(round).Spent, Is.False);
        Assert.That(entMan.HasComponent<SpentCasingComponent>(round), Is.False);
        Assert.That(entMan.HasComponent<TimedDespawnComponent>(round), Is.False);
        Assert.That(hands.CanPickupAnyHand(user, round), Is.True);
    }

    private static EntityUid ShootRound(IEntityManager entMan, SharedGunSystem guns, Entity<GunComponent> gun, string prototype)
    {
        var round = entMan.SpawnEntity(prototype, entMan.GetComponent<TransformComponent>(gun).Coordinates);
        Shoot(entMan, guns, gun, round);
        return round;
    }

    private static void Shoot(IEntityManager entMan, SharedGunSystem guns, Entity<GunComponent> gun, EntityUid ammo)
    {
        var coordinates = entMan.GetComponent<TransformComponent>(gun).Coordinates;
        guns.Shoot(gun, ammo, coordinates, coordinates.Offset(new Vector2(1f, 0f)), out _);
    }

    private static void AssertSpentCasing(
        IEntityManager entMan,
        IGameTiming timing,
        SharedHandsSystem hands,
        EntityUid user,
        EntityUid casing)
    {
        var spent = entMan.GetComponent<SpentCasingComponent>(casing);
        var lifetime = entMan.GetComponent<TimedDespawnComponent>(casing).Lifetime;
        var age = CasingAge(entMan, timing, casing);
        Assert.That(spent.DespawnSeconds, Is.EqualTo(DespawnSeconds));
        Assert.That(lifetime, Is.EqualTo(spent.DespawnSeconds - age).Within(LifetimeTickTolerance));
        Assert.That(hands.CanPickupAnyHand(user, casing), Is.False);
    }

    private static float CasingAge(IEntityManager entMan, IGameTiming timing, EntityUid casing)
    {
        var spentAt = entMan.GetComponent<SpentCasingComponent>(casing).SpentAt;
        return (float)(timing.CurTime - spentAt).TotalSeconds;
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
