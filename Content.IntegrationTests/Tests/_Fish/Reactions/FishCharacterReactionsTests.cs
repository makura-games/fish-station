#nullable enable
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Fish.Crit;
using Content.Shared._Fish.Reactions;
using Content.Shared.Atmos.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.Reactions;

[TestOf(typeof(SharedFishCharacterReactionsSystem))]
public sealed class FishCharacterReactionsTests : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";
    private static readonly EntProtoId TargetProtoId = "MobHuman";
    private static readonly ProtoId<DamageTypePrototype> BluntId = "Blunt";

    [Test]
    public async Task OrganicHasReactionsAndCritComponents()
    {
        await AddAtmosphere();
        await SpawnTarget(TargetProtoId);

        Assert.Multiple(() =>
        {
            Assert.That(HasComp<FishCharacterReactionsComponent>(), Is.True);
            Assert.That(HasComp<FishComplexCritComponent>(), Is.True);
            Assert.That(HasComp<FishTraumaStatusComponent>(), Is.True);
            Assert.That(HasComp<FlammableComponent>(), Is.True);
        });
    }

    [Test]
    public async Task SevereLegImpairmentBlocksStand()
    {
        var traumaSys = SEntMan.System<SharedFishTraumaSystem>();

        await AddAtmosphere();
        await SpawnTarget(TargetProtoId);

        await Server.WaitPost(() =>
        {
            traumaSys.ApplyLegImpairment(STarget!.Value, 0.95f);
        });

        var trauma = Comp<FishTraumaStatusComponent>();
        Assert.That(traumaSys.CanAttemptStand((STarget!.Value, trauma), out var reason), Is.False);
        Assert.That(reason, Is.EqualTo("fish-trauma-cant-stand-legs"));
    }

    [Test]
    public async Task BlastMarkerAppliesConcussion()
    {
        var traumaSys = SEntMan.System<SharedFishTraumaSystem>();

        await AddAtmosphere();
        await SpawnTarget(TargetProtoId);

        await Server.WaitPost(() =>
        {
            // Values must be set before ComponentStartup: OnBlastedStartup reads ThrowSpeed.
            SEntMan.AddComponent(STarget!.Value, new FishBlastedComponent
            {
                ThrowSpeed = 10f,
                Expires = TimeSpan.FromSeconds(9999), // не истекает за время теста
            });
        });

        var trauma = Comp<FishTraumaStatusComponent>();
        Assert.That(trauma.ConcussionSeverity, Is.GreaterThan(0f), "Primary blast должен дать контузию");
        Assert.That(trauma.LastBlastForce, Is.EqualTo(10f).Within(0.01f));
        Assert.That(traumaSys.CanAttemptStand((STarget!.Value, trauma), out _), Is.True);
    }

    [Test]
    public async Task DeathClearsTransientCritEffects()
    {
        var damageable = SEntMan.System<DamageableSystem>();
        var thresholds = SEntMan.System<MobThresholdSystem>();
        var reactions = SEntMan.System<SharedFishCharacterReactionsSystem>();

        await AddAtmosphere();
        await SpawnTarget(TargetProtoId);

        await Server.WaitPost(() =>
        {
            SEntMan.EnsureComponent<FishCritShockComponent>(STarget!.Value);
            SEntMan.EnsureComponent<FishLoseBreathComponent>(STarget.Value);
            SEntMan.EnsureComponent<FishCritUnconsciousComponent>(STarget.Value);

            var dead = thresholds.GetThresholdForState(STarget.Value, MobState.Dead);
            damageable.SetDamage((STarget.Value, Comp<DamageableComponent>()),
                new DamageSpecifier(ProtoMan.Index(BluntId), dead));
        });
        await RunTicks(5);

        Assert.That(Comp<MobStateComponent>().CurrentState, Is.EqualTo(MobState.Dead));

        // Shock / losebreath / unconscious должны очиститься обработчиком смерти.
        Assert.That(HasComp<FishCritUnconsciousComponent>(), Is.False);
        Assert.That(HasComp<FishLoseBreathComponent>(), Is.False);
        Assert.That(HasComp<FishCritShockComponent>(), Is.False);
    }

    [Test]
    public async Task SoftCritStandSuppressedWhileOnFire()
    {
        await AddAtmosphere();
        await SpawnTarget(TargetProtoId);

        await Server.WaitPost(() =>
        {
            var flam = Comp<FlammableComponent>();
            flam.OnFire = true;
            flam.FireStacks = 3f;
        });

        Assert.That(
            SharedFishCharacterReactionsSystem.ShouldSuppressSoftCritStand(SEntMan, STarget!.Value),
            Is.True);
    }
}
