#nullable enable
using System.Numerics;
using Content.Shared._Sunrise.BluespaceArtillery;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.IntegrationTests.Tests._Fish.Artillery;

[TestFixture]
[TestOf(typeof(BluespaceArtilleryDesignatorComponent))]
public sealed class FishBluespaceArtilleryDesignatorTimingTest
{
    [Test]
    public async Task DesignatorDefaults_MatchAdjustedTimingAndLaserScale()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var defaults = new BluespaceArtilleryDesignatorComponent();
            Assert.That(defaults.TargetingDelay, Is.EqualTo(TimeSpan.FromSeconds(3)));
            Assert.That(defaults.StrikeDelay, Is.EqualTo(TimeSpan.FromSeconds(9)));

            var designator = entMan.SpawnEntity("BluespaceArtilleryDesignator", MapCoordinates.Nullspace);
            Assert.That(entMan.TryGetComponent(designator, out BluespaceArtilleryDesignatorComponent? designatorComp), Is.True);
            Assert.That(designatorComp!.TargetingDelay, Is.EqualTo(TimeSpan.FromSeconds(3)));
            Assert.That(designatorComp.StrikeDelay, Is.EqualTo(TimeSpan.FromSeconds(9)));
            entMan.DeleteEntity(designator);

            var marker = entMan.SpawnEntity("EffectBluespaceArtilleryTarget", MapCoordinates.Nullspace);
            Assert.That(entMan.TryGetComponent(marker, out TimedDespawnComponent? despawn), Is.True);
            Assert.That(despawn!.Lifetime, Is.EqualTo(12f));
            entMan.DeleteEntity(marker);

            var admin = entMan.SpawnEntity("AdminBluespaceArtilleryDesignator", MapCoordinates.Nullspace);
            Assert.That(entMan.TryGetComponent(admin, out BluespaceArtilleryDesignatorComponent? adminComp), Is.True);
            Assert.That(adminComp!.TargetingDelay, Is.EqualTo(TimeSpan.Zero));
            Assert.That(adminComp.StrikeDelay, Is.EqualTo(TimeSpan.FromSeconds(9)));
            entMan.DeleteEntity(admin);
        });

        // Sprite — клиентский компонент; scale читаем из прототипа без спавна, чтобы не портить pool.
        await client.WaitAssertion(() =>
        {
            EntProtoId markerProto = "EffectBluespaceArtilleryTarget";
            var proto = client.ProtoMan.Index(markerProto);
            Assert.That(proto.TryComp(out SpriteComponent? sprite, client.EntMan.ComponentFactory), Is.True);
            Assert.That(sprite![0].Scale, Is.EqualTo(new Vector2(1f, 1f)));
        });

        await pair.CleanReturnAsync();
    }
}
