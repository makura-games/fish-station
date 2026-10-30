#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.Server._Sunrise.BloodCult;
using Content.Server._Sunrise.BloodCult.GameRule;
using Content.Server._Sunrise.BloodCult.Items.Systems;
using Content.Server._Sunrise.BloodCult.Juggernaut;
using Content.Server._Sunrise.BloodCult.Runes.Comps;
using Content.Server._Sunrise.BloodCult.Runes.Systems;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared._Sunrise.BloodCult.Runes;
using Content.Shared._Sunrise.BloodCult.UI;
// Fish-start: использование ванильного NightVision вместо удаленного ToggleableNightVision
// using Content.Shared._Sunrise.NightVision.Components;
// using Content.Shared._Fish.Vision;
using Content.Shared.Overlays;
// Fish-end
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Timing;
using Content.Shared.Timing.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Sunrise.BloodCult;

/// <summary>
/// Регрессии по ревью PR #368 (blood cult improvements).
/// </summary>
[TestFixture]
public sealed class BloodCultReviewTests
{
    [Test]
    public void CultBloodReagents_IncludeRacialBloodTypes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CultBloodSpellSystem.IsCultBloodReagent("Blood"), Is.True);
            Assert.That(CultBloodSpellSystem.IsCultBloodReagent("Slime"), Is.True);
            Assert.That(CultBloodSpellSystem.IsCultBloodReagent("CopperBlood"), Is.True); // арахнид
            Assert.That(CultBloodSpellSystem.IsCultBloodReagent("FluorosulfuricAcidHumanoidXeno"), Is.True); // ксено
            Assert.That(CultBloodSpellSystem.IsCultBloodReagent("FluorosulfuricAcidPredator"), Is.True); // яутжа
            Assert.That(CultBloodSpellSystem.IsCultBloodReagent("Water"), Is.False);
        });
    }

    [Test]
    public async Task CultistEquivalent_OnlyCultistAndConstruct_NotSoulShard()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var cult = server.System<BloodCultSystem>();

        EntityUid cultist = default;
        EntityUid construct = default;
        EntityUid shard = default;
        EntityUid civilian = default;

        await server.WaitPost(() =>
        {
            cultist = server.EntMan.Spawn("MobHuman", map.MapCoords);
            server.EntMan.EnsureComponent<BloodCultistComponent>(cultist);

            construct = server.EntMan.Spawn("MobHuman", map.MapCoords);
            server.EntMan.EnsureComponent<ConstructComponent>(construct);

            shard = server.EntMan.Spawn("SoulShardGhost", map.MapCoords);
            civilian = server.EntMan.Spawn("MobHuman", map.MapCoords);
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(cult.IsCultistEquivalent(cultist), Is.True);
                Assert.That(cult.IsCultistEquivalent(construct), Is.True);
                Assert.That(cult.IsCultistEquivalent(shard), Is.False);
                Assert.That(cult.IsCultistEquivalent(civilian), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NightVision_OnlyOnConstructs_NotHumanCultists()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid cultist = default;
        EntityUid construct = default;

        await server.WaitPost(() =>
        {
            cultist = server.EntMan.Spawn("MobHuman", map.MapCoords);
            server.EntMan.EnsureComponent<BloodCultistComponent>(cultist);

            construct = server.EntMan.Spawn("JuggernautConstruct", map.MapCoords);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                // Fish-start: проверка ванильного NightVision
                Assert.That(server.EntMan.HasComponent<NightVisionComponent>(cultist), Is.False);
                Assert.That(server.EntMan.HasComponent<NightVisionComponent>(construct), Is.True);
                // Fish-end
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RuneDraw_AllowedOnGridWithoutOwningStation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var cult = server.System<BloodCultSystem>();

        EntityUid drawer = default;

        await server.WaitPost(() =>
        {
            // CreateTestMap даёт грид без StationMember — как шаттл/планетойд.
            drawer = server.EntMan.Spawn("MobHuman", map.MapCoords);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(cult.IsAllowedToDraw(drawer), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SummonRitual_DoAfterRunsOnTarget_WithTwoTileThreshold()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid summoner = default;
        EntityUid target = default;
        EntityUid rune = default;

        await server.WaitPost(() =>
        {
            summoner = server.EntMan.Spawn("MobHuman", map.MapCoords);
            target = server.EntMan.Spawn("MobHuman", map.MapCoords);
            rune = server.EntMan.Spawn("SummoningRune", map.MapCoords);

            server.EntMan.EnsureComponent<BloodCultistComponent>(summoner);
            server.EntMan.EnsureComponent<BloodCultistComponent>(target);

            var provider = server.EntMan.EnsureComponent<CultRuneSummoningProviderComponent>(summoner);
            provider.BaseRune = rune;

            // На всякий случай, если прототип без них.
            server.EntMan.EnsureComponent<PullableComponent>(target);
            server.EntMan.EnsureComponent<CuffableComponent>(target);
            server.EntMan.EnsureComponent<DoAfterComponent>(target);
            server.EntMan.EnsureComponent<DoAfterComponent>(summoner);
        });

        await server.WaitPost(() =>
        {
            var msg = new SummonCultistListWindowItemSelectedMessage((int)target, 0)
            {
                Actor = summoner,
            };
            server.EntMan.EventBus.RaiseLocalEvent(summoner, (object)msg);
        });

        await server.WaitAssertion(() =>
        {
            var targetDoAfter = server.EntMan.GetComponent<DoAfterComponent>(target);
            var summonerDoAfter = server.EntMan.GetComponent<DoAfterComponent>(summoner);

            Assert.That(targetDoAfter.DoAfters, Is.Not.Empty, "DoAfter должен висеть на цели ритуала");
            Assert.That(summonerDoAfter.DoAfters, Is.Empty, "DoAfter не должен висеть на вызывающем");

            var doAfter = targetDoAfter.DoAfters.Values.First();
            Assert.Multiple(() =>
            {
                Assert.That(doAfter.Args.User, Is.EqualTo(target));
                Assert.That(doAfter.Args.EventTarget, Is.EqualTo(summoner));
                Assert.That(doAfter.Args.BreakOnMove, Is.True);
                Assert.That(doAfter.Args.MovementThreshold, Is.EqualTo(2f));
                Assert.That(doAfter.Args.BreakOnDamage, Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task JuggernautHammer_ResetsUseDelayOnMeleeHit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid juggernaut = default;
        EntityUid hammer = default;
        EntityUid victim = default;

        await server.WaitPost(() =>
        {
            juggernaut = server.EntMan.Spawn("MobHuman", map.MapCoords);
            server.EntMan.EnsureComponent<JuggernautComponent>(juggernaut);

            hammer = server.EntMan.Spawn("HammerJuggernaut", map.MapCoords);
            server.EntMan.EnsureComponent<JuggernautHammerComponent>(hammer);
            victim = server.EntMan.Spawn("MobHuman", map.MapCoords);
        });

        await server.WaitPost(() =>
        {
            var hit = new MeleeHitEvent([victim], juggernaut, hammer, new DamageSpecifier(), null)
            {
                IsHit = true,
            };
            server.EntMan.EventBus.RaiseLocalEvent(hammer, hit);
        });

        await server.WaitAssertion(() =>
        {
            var delay = server.System<UseDelaySystem>();
            Assert.That(delay.IsDelayed(hammer), Is.True, "После удара молотом должен стартовать UseDelay");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GodSummonRitual_AllowsThreeTilesOfMovement()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid rune = default;
        var cultists = new HashSet<EntityUid>();

        await server.WaitPost(() =>
        {
            // Fish-start: подготовка ритуала — жертва через BloodCultRuleSystem (RA0002 Access)
            var rule = server.EntMan.Spawn("Paper", map.MapCoords);
            var ruleComp = server.EntMan.EnsureComponent<BloodCultRuleComponent>(rule);
            server.EntMan.EnsureComponent<GameRuleComponent>(rule);

            user = server.EntMan.Spawn("MobHuman", map.MapCoords);
            server.EntMan.EnsureComponent<BloodCultistComponent>(user);
            server.EntMan.EnsureComponent<DoAfterComponent>(user);

            var victim = server.EntMan.Spawn("MobHuman", map.MapCoords);
            var cultRule = server.System<BloodCultRuleSystem>();
            Assert.That(cultRule.AddSpecificCultTarget(victim, ruleComp), Is.True);
            Assert.That(cultRule.MarkCultTargetKilled(victim, ruleComp), Is.True);

            rune = server.EntMan.Spawn("ApocalypseRune", map.MapCoords);
            var apoc = server.EntMan.GetComponent<CultRuneApocalypseComponent>(rune);
            apoc.SummonMinCount = 1;

            cultists.Add(user);
            // Fish-end
        });

        await server.WaitPost(() =>
        {
            var ev = new CultRuneInvokeEvent(rune, user, cultists);
            server.EntMan.EventBus.RaiseLocalEvent(rune, ev);
        });

        await server.WaitAssertion(() =>
        {
            var doAfterComp = server.EntMan.GetComponent<DoAfterComponent>(user);
            Assert.That(doAfterComp.DoAfters, Is.Not.Empty, "Ритуал призыва Бога должен стартовать DoAfter");

            var doAfter = doAfterComp.DoAfters.Values.First();
            Assert.Multiple(() =>
            {
                Assert.That(doAfter.Args.BreakOnMove, Is.True);
                Assert.That(doAfter.Args.MovementThreshold, Is.EqualTo(3f));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TargetsKill_EmptyListIsNotComplete()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid victim = default;
        BloodCultRuleComponent ruleComp = default!;

        await server.WaitPost(() =>
        {
            var rule = server.EntMan.Spawn("Paper", map.MapCoords);
            ruleComp = server.EntMan.EnsureComponent<BloodCultRuleComponent>(rule);
            server.EntMan.EnsureComponent<GameRuleComponent>(rule);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(
                server.System<BloodCultRuleSystem>().TargetsKill(),
                Is.False,
                "Пустой список целей не должен считаться выполненным");
        });

        await server.WaitPost(() =>
        {
            victim = server.EntMan.Spawn("MobHuman", map.MapCoords);
            Assert.That(server.System<BloodCultRuleSystem>().AddSpecificCultTarget(victim, ruleComp), Is.True);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<BloodCultRuleSystem>().TargetsKill(), Is.False);
        });

        await server.WaitPost(() =>
        {
            Assert.That(server.System<BloodCultRuleSystem>().MarkCultTargetKilled(victim, ruleComp), Is.True);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<BloodCultRuleSystem>().TargetsKill(), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
