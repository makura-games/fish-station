#nullable enable
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;

namespace Content.IntegrationTests.Tests._Fish.Silicons.Borgs;

/// <summary>
/// Проверяет, что Structural-урон по киборгам конвертируется в Blunt и не копится отдельно.
/// </summary>
[TestFixture]
public sealed class FishBorgStructuralDamageTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task Cyborg_StructuralDamage_ConvertsToBlunt()
    {
        await Server.WaitAssertion(() =>
        {
            var borg = SSpawn("BorgChassisGeneric");
            var damageable = Server.System<DamageableSystem>();

            // Через обычный урон (Silicon Structural: 0.9 и BorgArmorSilicon Structural: 0.2 -> 1.8 Blunt).
            var normal = new DamageSpecifier { DamageDict = { ["Structural"] = 10 } };
            damageable.TryChangeDamage(borg, normal, ignoreVariance: true);

            var all = damageable.GetAllDamage(borg);
            Assert.Multiple(() =>
            {
                Assert.That(all.DamageDict.GetValueOrDefault("Structural"), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(all.DamageDict.GetValueOrDefault("Blunt"), Is.EqualTo(FixedPoint2.New(1.8)));
            });

            // При ignoreResistances: true (например, молния) Structural тоже уходит в Blunt.
            var bypass = new DamageSpecifier { DamageDict = { ["Structural"] = 5 } };
            damageable.TryChangeDamage(borg, bypass, ignoreResistances: true, ignoreVariance: true);

            all = damageable.GetAllDamage(borg);
            Assert.Multiple(() =>
            {
                Assert.That(all.DamageDict.GetValueOrDefault("Structural"), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(all.DamageDict.GetValueOrDefault("Blunt"), Is.EqualTo(FixedPoint2.New(6.8)));
            });
        });
    }
}
