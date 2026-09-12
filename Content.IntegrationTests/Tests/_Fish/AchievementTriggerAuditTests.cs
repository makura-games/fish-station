using System;
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Shared._Fish.Achievements;
using Content.Shared._Sunrise.StatsBoard;
using Content.Shared._Sunrise.Storyteller;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Prototypes;
using Content.Shared.Mind;
using Content.Shared.Players;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Fish;

/// <summary>
/// Runtime audit: каждый прототип имеет известный handler и unlock path.
/// </summary>
[TestFixture]
public sealed class AchievementTriggerAuditTests
{
    private static readonly HashSet<string> HandledConditions = new()
    {
        AchievementConditionKeys.FirstLateJoin,
        AchievementConditionKeys.JobPlay,
        AchievementConditionKeys.RoundEndAlive,
        AchievementConditionKeys.RoundSurvive,
        AchievementConditionKeys.Counter,
        AchievementConditionKeys.AntagWin,
        AchievementConditionKeys.Death,
        AchievementConditionKeys.SlipDeath,
        AchievementConditionKeys.Kill,
        AchievementConditionKeys.DamageDealt,
        AchievementConditionKeys.Heal,
        AchievementConditionKeys.Craft,
        AchievementConditionKeys.ItemPickup,
        AchievementConditionKeys.Interaction,
        AchievementConditionKeys.StationEvent,
        AchievementConditionKeys.ShuttleArrive,
        AchievementConditionKeys.Explosion,
        AchievementConditionKeys.BecameGhost,
        AchievementConditionKeys.ItemIngest,
        AchievementConditionKeys.AntagSelected,
        AchievementConditionKeys.ObjectiveComplete,
        AchievementConditionKeys.PlaytimeMinutes,
        AchievementConditionKeys.RoleAdded,
        AchievementConditionKeys.Defibrillate,
        AchievementConditionKeys.Surgery,
        AchievementConditionKeys.GunShot,
        AchievementConditionKeys.Examine,
        AchievementConditionKeys.SingularityConsumed,
        AchievementConditionKeys.Succumb,
        AchievementConditionKeys.Emote,
        AchievementConditionKeys.AiLawChanges,
        AchievementConditionKeys.ReagentMetabolize,
        AchievementConditionKeys.ChasmFall,
        AchievementConditionKeys.GavelStrike,
        AchievementConditionKeys.TilePry,
        AchievementConditionKeys.Gibbed,
        AchievementConditionKeys.SlipDeath,
    };

    private static readonly HashSet<string> InherentlySpecificConditions = new()
    {
        AchievementConditionKeys.BecameGhost,
        AchievementConditionKeys.SingularityConsumed,
        AchievementConditionKeys.Succumb,
        AchievementConditionKeys.FirstLateJoin,
        AchievementConditionKeys.AntagWin,
        AchievementConditionKeys.RoundEndAlive,
        AchievementConditionKeys.RoundSurvive,
        AchievementConditionKeys.ShuttleArrive,
        AchievementConditionKeys.ChasmFall,
        AchievementConditionKeys.Gibbed,
        AchievementConditionKeys.SlipDeath,
    };

    private static readonly HashSet<string> SeedFullyImplemented = new()
    {
        "FishAchFirstBreath",
        "FishAchStillStanding",
        "FishAchBananaRequiem",
        "FishAchCentcommTourist",
        "FishAchHabitualSurvivor",
    };

    [Test]
    public async Task Audit_AllAchievements_HaveKnownConditionAndUnlockPath()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoMan = pair.Server.ResolveDependency<IPrototypeManager>();

        var all = protoMan.EnumeratePrototypes<AchievementPrototype>().ToList();
        Assert.That(all, Has.Count.GreaterThan(0));

        var manual = all.Where(p => p.Condition == AchievementConditionKeys.Manual).ToList();
        Assert.That(manual, Has.Count.EqualTo(0), "Manual catalog stubs must be removed, not kept");

        foreach (var proto in all)
        {
            Assert.That(HandledConditions.Contains(proto.Condition), Is.True,
                $"{proto.ID}: no handler for condition {proto.Condition}");

            if (InherentlySpecificConditions.Contains(proto.Condition))
                continue;

            Assert.That(
                proto.AllowGenericTrigger || proto.ConditionParams.Count > 0,
                Is.True,
                $"{proto.ID}: missing unlock path (allowGenericTrigger or conditionParams)");
        }

        var seed = all.Where(p => SeedFullyImplemented.Contains(p.ID)).ToList();
        Assert.That(seed, Has.Count.EqualTo(SeedFullyImplemented.Count));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TestRoleAdded_ConcurrentRoleAddition_DoesNotThrowCollectionModified()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Fresh = true,
            Dirty = true,
            DummyTicker = false,
            Connected = true
        });

        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var sPlayerMan = server.ResolveDependency<IPlayerManager>();
        var roleSystem = entMan.System<SharedRoleSystem>();

        var session = sPlayerMan.Sessions.Single();
        var mindId = session.ContentData()!.Mind!.Value;

        await server.WaitAssertion(() =>
        {
            Assert.DoesNotThrow(() =>
            {
                // Добавляем роли подряд: OnRoleAdded асинхронно обрабатывает ContainedEntities,
                // не должно выбрасываться InvalidOperationException (Collection was modified).
                roleSystem.MindAddRole(mindId, "MindRoleTraitor");
                roleSystem.MindAddJobRole(mindId, jobPrototype: "Passenger");
                roleSystem.MindAddRole(mindId, "MindRoleDragon");
            });
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var mind = entMan.GetComponent<MindComponent>(mindId);
            Assert.That(mind.MindRoleContainer.ContainedEntities.Count, Is.GreaterThanOrEqualTo(3));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TestRoundEnd_OccurredEventsSnapshot_SafeWhenRoundRestartClears()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Fresh = true,
            Dirty = true,
            DummyTicker = false,
            Connected = true
        });

        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            Assert.DoesNotThrow(() =>
            {
                // Запускаем несколько правил станции
                var ev1 = new GameRuleStartedEvent(EntityUid.Invalid, "DragonSpawn");
                entMan.EventBus.RaiseLocalEvent(EntityUid.Invalid, ref ev1, true);
                var ev2 = new GameRuleStartedEvent(EntityUid.Invalid, "IonStorm");
                entMan.EventBus.RaiseLocalEvent(EntityUid.Invalid, ref ev2, true);

                // Завершение раунда: снимок массива событий защищает от параллельного/последующего Clear
                var endEv = new RoundEndMessageEvent(
                    gamemodeTitle: "test",
                    roundEndText: "test round ended",
                    roundDuration: TimeSpan.FromMinutes(5),
                    roundId: 1,
                    playerCount: 1,
                    allPlayersEndInfo: Array.Empty<RoundEndMessageEvent.RoundEndPlayerInfo>(),
                    roundEndStats: string.Empty,
                    statisticEntries: Array.Empty<SharedStatisticEntry>(),
                    storytellerName: null,
                    storytellerHistory: Array.Empty<StorytellerHistoryEntry>(),
                    restartSound: null);
                entMan.EventBus.RaiseEvent(EventSource.Local, endEv);

                // Вызов RoundRestartCleanupEvent сбрасывает состояние в AchievementConditionSystem во время асинхронной обработки
                var restartEv = new RoundRestartCleanupEvent();
                entMan.EventBus.RaiseEvent(EventSource.Local, restartEv);
            });
        });

        await pair.RunTicksSync(10);
        await pair.CleanReturnAsync();
    }
}
