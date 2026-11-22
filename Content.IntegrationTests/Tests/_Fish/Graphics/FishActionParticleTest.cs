using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Fish.Effects;
using Content.Shared.Chemistry.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Fish.Graphics;

public sealed class FishActionParticleTest : InteractionTest
{
    [Test]
    public async Task BurstIsLocalAndExpires()
    {
        await Server.WaitPost(() =>
        {
            var particles = Server.System<FishActionParticleSystem>();
            particles.Emit(SPlayer, FishParticleKind.BloodSpray, new Solution());
            particles.Emit(SPlayer, FishParticleKind.Weld);
        });
        await RunTicks(3);

        int Count(IEntityManager entities, string prototype) => entities.EntityQuery<MetaDataComponent>()
            .Count(meta => meta.EntityPrototype?.ID == prototype);

        await Server.WaitAssertion(() => Assert.That(Count(SEntMan, "FishWeldParticle"), Is.Zero));
        await Client.WaitAssertion(() =>
        {
            Assert.That(Count(CEntMan, "FishWeldParticle"), Is.EqualTo(8));
            Assert.That(Count(CEntMan, "FishBloodParticle"), Is.Zero);
        });
        await RunTicks((int) (1 / TickPeriod) + 1);
        await Client.WaitAssertion(() => Assert.That(Count(CEntMan, "FishWeldParticle"), Is.Zero));
    }
}
