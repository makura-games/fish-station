#nullable enable
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Fish.Crit;
using Content.Shared._Fish.Crit;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.Crit;

/// <summary>
/// Интеграционные тесты расширенного Crit.
/// </summary>
[TestOf(typeof(FishComplexCritSystem))]
public sealed class FishComplexCritTests : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private static readonly EntProtoId TargetProtoId = "MobHuman";
    private static readonly ProtoId<DamageTypePrototype> BluntDamageTypeId = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> AsphyxiationId = "Asphyxiation";

    [Test]
    public async Task EnterCritZoneAppliesComponentBehavior()
    {
        var damageableSystem = SEntMan.System<DamageableSystem>();
        var mobThresholds = SEntMan.System<MobThresholdSystem>();
        var critSystem = SEntMan.System<SharedFishComplexCritSystem>();

        await AddAtmosphere();
        await SpawnTarget(TargetProtoId);

        Assert.That(SEntMan.HasComponent<FishComplexCritComponent>(STarget!.Value), Is.True,
            "MobHuman должен иметь FishComplexCrit");

        var critThreshold = mobThresholds.GetThresholdForState(STarget.Value, MobState.Critical);
        var damage = new DamageSpecifier(ProtoMan.Index(BluntDamageTypeId), critThreshold);
        var damageable = Comp<DamageableComponent>();

        await Server.WaitPost(() => damageableSystem.SetDamage((STarget.Value, damageable), damage));
        await RunTicks(5);

        Assert.That(critSystem.IsInCritZone(STarget.Value), Is.True, "Персонаж должен быть в зоне Crit");
        Assert.That(Comp<MobStateComponent>().CurrentState, Is.EqualTo(MobState.Critical));
    }

    [Test]
    public async Task CritTickAppliesAsphyxiation()
    {
        var damageableSystem = SEntMan.System<DamageableSystem>();
        var mobThresholds = SEntMan.System<MobThresholdSystem>();

        await AddAtmosphere();
        await SpawnTarget(TargetProtoId);

        var critThreshold = mobThresholds.GetThresholdForState(STarget!.Value, MobState.Critical);
        var damage = new DamageSpecifier(ProtoMan.Index(BluntDamageTypeId), critThreshold + 10);
        var damageable = Comp<DamageableComponent>();

        await Server.WaitPost(() => damageableSystem.SetDamage((STarget.Value, damageable), damage));

#pragma warning disable CS0618
        var oxyBefore = damageableSystem.GetAllDamage(STarget.Value).DamageDict.TryGetValue(AsphyxiationId, out var before)
            ? before
            : FixedPoint2.Zero;
#pragma warning restore CS0618

        await RunTicks(150);

#pragma warning disable CS0618
        var oxyAfter = damageableSystem.GetAllDamage(STarget.Value).DamageDict.TryGetValue(AsphyxiationId, out var after)
            ? after
            : FixedPoint2.Zero;
#pragma warning restore CS0618

        Assert.That(oxyAfter, Is.GreaterThan(oxyBefore),
            "В крите должен накапливаться Asphyxiation (adjustOxyLoss(1) за тик)");
    }

    [Test]
    public async Task ForceShockAndHeartFailureComponents()
    {
        await AddAtmosphere();
        await SpawnTarget(TargetProtoId);

        await Server.WaitPost(() =>
        {
            SEntMan.EnsureComponent<FishCritShockComponent>(STarget!.Value);
            SEntMan.EnsureComponent<FishHeartFailureComponent>(STarget.Value);
            SEntMan.EnsureComponent<FishCardiacArrestComponent>(STarget.Value);
        });
        await RunTicks(2);

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.HasComponent<FishCritShockComponent>(STarget!.Value), Is.True);
            Assert.That(SEntMan.HasComponent<FishHeartFailureComponent>(STarget.Value), Is.True);
            Assert.That(SEntMan.HasComponent<FishCardiacArrestComponent>(STarget.Value), Is.True);
        });

        // Повторный EnsureComp не создаёт дубликатов компонентов.
        await Server.WaitPost(() =>
        {
            SEntMan.EnsureComponent<FishCritShockComponent>(STarget!.Value);
            SEntMan.EnsureComponent<FishHeartFailureComponent>(STarget.Value);
        });
        await RunTicks(1);

        Assert.That(SEntMan.HasComponent<FishCritShockComponent>(STarget!.Value), Is.True);
    }

    [Test]
    public async Task ClearCardiacArrestViaApi()
    {
        var critSystem = SEntMan.System<SharedFishComplexCritSystem>();

        await AddAtmosphere();
        await SpawnTarget(TargetProtoId);

        await Server.WaitPost(() =>
        {
            Assert.That(critSystem.TrySetHeartAttack(STarget!.Value, true), Is.True);
            Assert.That(critSystem.IsUndergoingCardiacArrest(STarget.Value), Is.True);
            Assert.That(critSystem.TrySetHeartAttack(STarget.Value, false), Is.True);
        });
        await RunTicks(2);

        Assert.That(critSystem.IsUndergoingCardiacArrest(STarget!.Value), Is.False);
    }

    [Test]
    public async Task ExitCritWhenHealed()
    {
        var damageableSystem = SEntMan.System<DamageableSystem>();
        var mobThresholds = SEntMan.System<MobThresholdSystem>();
        var critSystem = SEntMan.System<SharedFishComplexCritSystem>();

        await AddAtmosphere();
        await SpawnTarget(TargetProtoId);

        var critThreshold = mobThresholds.GetThresholdForState(STarget!.Value, MobState.Critical);
        var damageable = Comp<DamageableComponent>();
        var damage = new DamageSpecifier(ProtoMan.Index(BluntDamageTypeId), critThreshold + 20);

        await Server.WaitPost(() => damageableSystem.SetDamage((STarget.Value, damageable), damage));
        await RunTicks(3);
        Assert.That(critSystem.IsInCritZone(STarget.Value), Is.True);

        await Server.WaitPost(() =>
            damageableSystem.SetDamage((STarget.Value, Comp<DamageableComponent>()), new DamageSpecifier()));
        await RunTicks(5);

        Assert.That(critSystem.IsInCritZone(STarget.Value), Is.False);
        Assert.That(Comp<MobStateComponent>().CurrentState, Is.EqualTo(MobState.Alive));
    }

    [Test]
    public async Task DeadThresholdNotRaisedAboveGib()
    {
        var mobThresholds = SEntMan.System<MobThresholdSystem>();

        await AddAtmosphere();
        await SpawnTarget(TargetProtoId);

        Assert.That(SEntMan.TryGetComponent<FishComplexCritComponent>(STarget!.Value, out var crit), Is.True);
        var dead = mobThresholds.GetThresholdForState(STarget.Value, MobState.Dead);

        // Dead остаётся ванильным (~200), иначе SetDamage(600) гибает сущность (Gib@Blunt400).
        Assert.That((float)dead, Is.LessThanOrEqualTo(400f),
            "Dead-порог не должен превышать порог Gib");
        Assert.That((float)dead, Is.EqualTo(crit!.DeadDamageThreshold).Within(0.1f));
    }

    [Test]
    public async Task EntityDeletionCleansCritState()
    {
        await AddAtmosphere();
        await SpawnTarget(TargetProtoId);

        await Server.WaitPost(() =>
        {
            SEntMan.EnsureComponent<FishCritShockComponent>(STarget!.Value);
            SEntMan.EnsureComponent<FishLoseBreathComponent>(STarget.Value);
            SEntMan.DeleteEntity(STarget.Value);
        });
        await RunTicks(2);

        Assert.Pass();
    }
}
