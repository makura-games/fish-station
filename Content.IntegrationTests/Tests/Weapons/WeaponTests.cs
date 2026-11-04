using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Input;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Weapons;

public sealed class WeaponTests : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman"; // The default test mob only has one hand
    private static readonly EntProtoId MobHuman = "MobHuman";
    private static readonly EntProtoId LightMachineGunL6 = "WeaponLightMachineGunL6";

    [Test]
    public async Task GunRequiresWieldTest()
    {
        var gunSystem = SEntMan.System<SharedGunSystem>();
        var damageSystem = SEntMan.System<DamageableSystem>();

        await AddAtmosphere(); // prevent the Urist from suffocating

        var urist = await SpawnTarget(MobHuman);
        var lmgNet = await PlaceInHands(LightMachineGunL6);
        var lmgEnt = ToServer(lmgNet);

        await Pair.RunSeconds(2f); // Guns have a cooldown when picking them up.

        Assert.That(HasComp<GunRequiresWieldComponent>(lmgNet),
            "Looks like you've removed the 'GunRequiresWield' component from the light machine gun." +
            "If this was intentional, please update WeaponTests.cs to reflect this change!");

        var startAmmo = gunSystem.GetAmmoCount(lmgEnt);
        var wieldComp = Comp<WieldableComponent>(lmgNet);

        Assert.That(startAmmo, Is.GreaterThan(0), "Light Machine Gun was spawned with no ammo!");
        Assert.That(wieldComp.Wielded, Is.False, "Light Machine Gun was spawned wielded!");

        await AttemptShoot(urist, false); // should fail due to not being wielded
        var updatedAmmo = gunSystem.GetAmmoCount(lmgEnt);

        Assert.That(updatedAmmo,
            Is.EqualTo(startAmmo),
            "Light Machine Gun discharged ammo when the weapon should not have fired!");
        Assert.That(damageSystem.GetTotalDamage(ToServer(urist)),
            Is.EqualTo(FixedPoint2.Zero),
            "Urist took damage when the weapon should not have fired!");

        await UseInHand();

        Assert.That(wieldComp.Wielded, Is.True, "Light Machine Gun failed to wield when interacted with!");

        await AttemptShoot(urist);
        updatedAmmo = gunSystem.GetAmmoCount(lmgEnt);
        // Sunrise-start
//        Assert.That(updatedAmmo, Is.EqualTo(startAmmo), "Light Machine Gun should keep ammo count until the bolt is cycled!");

//        await PressKey(ContentKeyFunctions.CockGun);
//        updatedAmmo = gunSystem.GetAmmoCount(lmgEnt);

//        Assert.That(updatedAmmo, Is.EqualTo(startAmmo - 1), "Light Machine Gun failed to discharge appropriate amount of ammo after cycling!");
        // Sunrise-end
        Assert.That(damageSystem.GetTotalDamage(ToServer(urist)),
            Is.GreaterThan(FixedPoint2.Zero),
            "Light Machine Gun was fired but urist sustained no damage!");
    }
}
