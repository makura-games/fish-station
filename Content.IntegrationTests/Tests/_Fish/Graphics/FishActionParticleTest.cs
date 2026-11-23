using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared._Fish.Effects;
using Content.Shared.CCVar;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Fish.Graphics;

public sealed class FishActionParticleTest : InteractionTest
{
    [TestCase("TableWood", 1, "FishWoodParticle", 0)]
    [TestCase("TableWood", 42, "FishWoodParticle", 6)]
    [TestCase("TableWood", 175, "FishWoodParticle", 6)]
    [TestCase("Window", 85, "FishGlassParticle", 7)]
    [TestCase("Window", 200, "FishGlassParticle", 7)]
    public async Task DamageEmitsDebrisOnDestruction(string prototype, int damage, string particle, int expected)
    {
        var target = await Spawn(prototype);
        await Server.WaitPost(() => Server.System<DamageableSystem>().TryChangeDamage(ToServer(target),
            new DamageSpecifier { DamageDict = { ["Blunt"] = damage } }, ignoreResistances: true, ignoreVariance: true));

        await RunTicks(3);
        var count = 0;
        await Client.WaitPost(() => count = CEntMan.EntityQuery<MetaDataComponent>()
            .Count(meta => meta.EntityPrototype?.ID == particle));
        Assert.That(count, Is.EqualTo(expected));
        await RunTicks((int) (1 / TickPeriod) + 1);
    }

    [Test]
    public async Task DisablingParticlesClearsBurstsAndAllowsReenabling()
    {
        var cfg = Client.CfgMan;
        var original = cfg.GetCVar(FishCVars.ActionParticlesEnabled);

        async Task EmitWeld()
        {
            await Server.WaitPost(() => Server.System<FishActionParticleSystem>().Emit(SPlayer, FishParticleKind.Weld));
            await RunTicks(3);
        }

        Task AssertWeldCount(int expected) => Client.WaitAssertion(() =>
            Assert.That(CEntMan.EntityQuery<MetaDataComponent>()
                .Count(meta => meta.EntityPrototype?.ID == "FishWeldParticle"), Is.EqualTo(expected)));

        try
        {
            await Client.WaitPost(() => cfg.SetCVar(FishCVars.ActionParticlesEnabled, true));
            await EmitWeld();
            await AssertWeldCount(8);

            await Client.WaitPost(() => cfg.SetCVar(FishCVars.ActionParticlesEnabled, false));
            await EmitWeld();
            await AssertWeldCount(0);

            await Client.WaitPost(() => cfg.SetCVar(FishCVars.ActionParticlesEnabled, true));
            await EmitWeld();
            await AssertWeldCount(8);
        }
        finally
        {
            await Client.WaitPost(() => cfg.SetCVar(FishCVars.ActionParticlesEnabled, original));
            await RunTicks((int) (1 / TickPeriod) + 1);
        }
    }

    [Test]
    public async Task HeldShakerEmitsBubbles()
    {
        var shaker = await PlaceInHands("DrinkShaker");
        await Server.WaitAssertion(() =>
        {
            var uid = ToServer(shaker);
            var solutions = Server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetMixableSolution(uid, out var solution, out _), Is.True);
            Assert.That(solutions.TryAddReagent(solution!.Value, "Water", 5), Is.True);
            Assert.That(Server.System<ReactionMixerSystem>().TryMix(uid, uid), Is.True);
        });
        await RunTicks(3);
        await Client.WaitAssertion(() => Assert.That(CEntMan.EntityQuery<MetaDataComponent>()
            .Count(meta => meta.EntityPrototype?.ID == "FishBubblesParticle"), Is.EqualTo(4)));
        await RunTicks((int) (1 / TickPeriod) + 1);
    }

    [Test]
    public async Task FireTransitionsEmitOnce()
    {
        await Server.WaitPost(() =>
        {
            var system = Server.System<FlammableSystem>();
            var flammable = SEntMan.EnsureComponent<FlammableComponent>(SPlayer);
            system.SetFireStacks(SPlayer, 3, flammable);
            system.Ignite(SPlayer, SPlayer, flammable);
            system.Ignite(SPlayer, SPlayer, flammable);
            Assert.That(system.TryExtinguish((SPlayer, flammable)), Is.True);
            Assert.That(system.TryExtinguish((SPlayer, flammable)), Is.False);
        });
        await RunTicks(3);
        await Client.WaitAssertion(() =>
        {
            var ids = CEntMan.EntityQuery<MetaDataComponent>().Select(meta => meta.EntityPrototype?.ID).ToArray();
            Assert.That(ids.Count(id => id == "FishIgnitionParticle"), Is.EqualTo(6));
            Assert.That(ids.Count(id => id == "FishSmokeParticle"), Is.EqualTo(4));
        });
        await RunTicks((int) (1 / TickPeriod) + 1);
    }

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
