using Content.IntegrationTests.Fixtures;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Spawners;

namespace Content.IntegrationTests.Tests._Fish.Graphics;

public sealed class FishStationEffectTest : GameTest
{
    [Test]
    public async Task AnimationFitsEffectLifetime()
    {
        var client = Pair.Client;
        var sprites = client.System<SpriteSystem>();
        string[] effects = ["EffectSparks", "EffectTeslaSparks", "EffectEmpPulse", "EffectEmpDisabled",
            "TeleportEffectSource", "TeleportEffectTarget"];

        await client.WaitAssertion(() =>
        {
            foreach (var id in effects)
            {
                var entity = client.EntMan.SpawnEntity(id, MapCoordinates.Nullspace);
                try
                {
                    var rsi = sprites.LayerGetEffectiveRsi(entity, 0);
                    Assert.That(rsi, Is.Not.Null, id);
                    var animation = rsi![sprites.LayerGetRsiState(entity, 0)];
                    Assert.That(animation.Delays.Length, Is.GreaterThan(1), id);
                    var lifetime = client.EntMan.GetComponent<TimedDespawnComponent>(entity).Lifetime;
                    Assert.That(animation.AnimationLength, Is.EqualTo(lifetime).Within(0.001f), id);
                }
                finally
                {
                    client.EntMan.DeleteEntity(entity);
                }
            }
        });
    }
}
