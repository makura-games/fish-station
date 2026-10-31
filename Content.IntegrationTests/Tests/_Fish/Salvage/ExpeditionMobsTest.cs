#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Fish.Salvage;
using Content.Server._Sunrise.Misc.ShiftedAsciiTableAccent;
using Content.Server.Ghost.Roles.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Fish.Salvage;

[TestFixture]
public sealed class ExpeditionMobsTest : GameTest
{
    private const string Goliath = "MobGoliath";
    private const string Pirate = "MobPirateT1Knife";

    /// <summary>
    /// По одному мобу из остальных веток, где шанс гост-роли был изменён: пираты T2 и T3, босс T3 и «советы».
    /// </summary>
    private static readonly string[] OtherBranches =
    [
        "MobPirateT2Cutlass",
        "MobPirateT3Medic",
        "MobPirateT3Boss",
        "MobSovietT1Knife",
        // Капитан: свою гост-роль без prob он наследует от MobSovietBase, где prob: 0.
        "MobSovietCaptain",
    ];

    /// <summary>
    /// Боссы экспедиций с гарантированной гост-ролью (без случайного шанса и без prob у родителей).
    /// MobRogueSiliconBossSkullHouse сюда не входит: у его роли prob: 0.35, и тест стал бы плавающим.
    /// </summary>
    private static readonly string[] GuaranteedGhostRoleBosses =
    [
        "MobRogueSiliconBoss",
        "MobDragonDungeon",
        "MobCarpRainbowDragon",
    ];

    /// <summary>
    /// Мобы экспедиций никогда не становятся гост-ролью и не получают AnomalyAccent.
    /// </summary>
    [Test]
    public async Task ExpeditionPiratesHaveNoGhostRoleAndNoAnomalyAccent()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await Pair.CreateTestMap();

        var mobs = new List<(EntityUid Uid, string Proto)>();

        await server.WaitPost(() =>
        {
            // Раньше шанс гост-роли у пиратов T1 был 10%, поэтому берём много, чтобы тест был надёжным.
            for (var i = 0; i < 60; i++)
            {
                mobs.Add((entMan.SpawnEntity(Pirate, testMap.GridCoords), Pirate));
            }

            // Остальные изменённые ветки: у каждой свой шанс гост-роли в прототипе.
            foreach (var proto in OtherBranches)
            {
                mobs.Add((entMan.SpawnEntity(proto, testMap.GridCoords), proto));
            }
        });

        // Гост-роль с вероятностью убирается отложенно, при инициализации карты.
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            foreach (var (mob, proto) in mobs)
            {
                Assert.That(entMan.HasComponent<GhostRoleComponent>(mob), Is.False, $"{proto} стал гост-ролью");
                Assert.That(entMan.HasComponent<AnomalyAccentComponent>(mob), Is.False, $"{proto} получил AnomalyAccent");
            }
        });
    }

    /// <summary>
    /// Радиусы обзора и агра мобов на карте экспедиции увеличиваются, а мобы на других картах не затрагиваются.
    /// </summary>
    [Test]
    public async Task BoostMobVisionScalesRadiiOnlyOnExpeditionMap()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await Pair.CreateTestMap();
        // Вторая карта, на которой моб не должен измениться.
        var otherMap = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            // При генерации экспедиции карта стоит на паузе, пока на ней появляются мобы.
            entMan.System<SharedMapSystem>().SetPaused(testMap.MapUid, true);

            var goliath = entMan.SpawnEntity(Goliath, testMap.GridCoords);
            var pirate = entMan.SpawnEntity(Pirate, testMap.GridCoords);
            var large = entMan.SpawnEntity(Pirate, testMap.GridCoords);
            var elsewhere = entMan.SpawnEntity(Goliath, otherMap.GridCoords);

            // У голиафа радиусы заданы в прототипе: обзор 6, агр 10. У пирата стандартные: 10 и 10.
            AssertRadii(entMan, goliath, 6f, 10f);
            AssertRadii(entMan, pirate, 10f, 10f);

            // Мобу с уже большими радиусами задаём их вручную, чтобы проверить предел.
            var largeBlackboard = entMan.GetComponent<HTNComponent>(large).Blackboard;
            largeBlackboard.SetValue("VisionRadius", 30f);
            largeBlackboard.SetValue("AggroVisionRadius", 40f);

            entMan.System<ExpeditionMobSystem>().BoostMobVision(testMap.MapUid);

            AssertRadii(entMan, goliath, 9f, 15f);
            AssertRadii(entMan, pirate, 15f, 15f);
            // Меньший радиус поднимается только до предела, а тот, что уже выше предела, остаётся прежним.
            AssertRadii(entMan, large, ExpeditionMobSystem.RadiusCap, 40f);
            // Моб вне карты экспедиции не затронут.
            AssertRadii(entMan, elsewhere, 6f, 10f);
        });

        // Пара очищает только последнюю созданную карту, первую убираем сами, чтобы не мешать следующим тестам.
        await server.WaitPost(() => entMan.System<SharedMapSystem>().DeleteMap(testMap.MapId));
    }

    /// <summary>
    /// Боссы с гарантированной гост-ролью теряют её на карте экспедиции, а на других картах роль остаётся.
    /// </summary>
    [Test]
    public async Task RemoveGhostRolesStripsBossesOnlyOnExpeditionMap()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await Pair.CreateTestMap();
        var otherMap = await Pair.CreateTestMap();

        var onExpedition = new List<(EntityUid Uid, string Proto)>();
        var elsewhere = new List<(EntityUid Uid, string Proto)>();

        await server.WaitPost(() =>
        {
            // При генерации экспедиции карта стоит на паузе, пока на ней появляются мобы.
            entMan.System<SharedMapSystem>().SetPaused(testMap.MapUid, true);

            foreach (var proto in GuaranteedGhostRoleBosses)
            {
                onExpedition.Add((entMan.SpawnEntity(proto, testMap.GridCoords), proto));
                elsewhere.Add((entMan.SpawnEntity(proto, otherMap.GridCoords), proto));
            }
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            // Проверка исходных данных: у всех боссов роль действительно есть, иначе тест ничего не доказывает.
            foreach (var (boss, proto) in onExpedition.Concat(elsewhere))
            {
                Assert.That(entMan.HasComponent<GhostRoleComponent>(boss), Is.True, $"{proto} без гост-роли до проверки");
            }

            entMan.System<ExpeditionMobSystem>().RemoveGhostRoles(testMap.MapUid);
        });

        // Компоненты снимаются отложенно.
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            foreach (var (boss, proto) in onExpedition)
            {
                Assert.That(entMan.HasComponent<GhostRoleComponent>(boss), Is.False, $"{proto} остался гост-ролью");
                Assert.That(entMan.HasComponent<GhostTakeoverAvailableComponent>(boss), Is.False,
                    $"{proto} остался доступен для захвата");
            }

            // На другой карте боссы не затронуты.
            foreach (var (boss, proto) in elsewhere)
            {
                Assert.That(entMan.HasComponent<GhostRoleComponent>(boss), Is.True, $"{proto} лишился роли на чужой карте");
            }
        });

        // Пара очищает только последнюю созданную карту, первую убираем сами, чтобы не мешать следующим тестам.
        await server.WaitPost(() => entMan.System<SharedMapSystem>().DeleteMap(testMap.MapId));
    }

    /// <summary>
    /// Возможность захвата снимается и у моба, у которого гост-роль уже удалилась.
    /// </summary>
    [Test]
    public async Task RemoveGhostRolesStripsTakeoverWithoutGhostRole()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await Pair.CreateTestMap();
        var mob = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            entMan.System<SharedMapSystem>().SetPaused(testMap.MapUid, true);
            mob = entMan.SpawnEntity(Pirate, testMap.GridCoords);
            entMan.RemoveComponent<GhostRoleComponent>(mob);
            entMan.EnsureComponent<GhostTakeoverAvailableComponent>(mob);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<GhostRoleComponent>(mob), Is.False);
            Assert.That(entMan.HasComponent<GhostTakeoverAvailableComponent>(mob), Is.True);

            entMan.System<ExpeditionMobSystem>().RemoveGhostRoles(testMap.MapUid);
        });

        // Компоненты снимаются отложенно.
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<GhostTakeoverAvailableComponent>(mob), Is.False);
        });
    }

    /// <summary>
    /// Проверяет, что у моба радиусы обзора и агра в блэкборде равны ожидаемым.
    /// </summary>
    private static void AssertRadii(IEntityManager entMan, EntityUid mob, float vision, float aggro)
    {
        var blackboard = entMan.GetComponent<HTNComponent>(mob).Blackboard;
        Assert.That(blackboard.GetValueOrDefault<float>("VisionRadius", entMan), Is.EqualTo(vision));
        Assert.That(blackboard.GetValueOrDefault<float>("AggroVisionRadius", entMan), Is.EqualTo(aggro));
    }
}
