#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Server._Fish.Salvage;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.Salvage.Expeditions;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Fish.Salvage;

[TestFixture]
public sealed class ExpeditionMobsTest : GameTest
{
    private const string Goliath = "MobGoliath";
    private const string Pirate = "MobPirateT1Knife";

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
    /// MapInit HTN на карте с SalvageExpedition поднимает vision; повторный BoostMobVision не умножает снова.
    /// </summary>
    [Test]
    public async Task HtnMapInitOnExpeditionAppliesVisionBoostIdempotently()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await Pair.CreateTestMap();
        var goliath = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            entMan.AddComponent<SalvageExpeditionComponent>(testMap.MapUid);
            goliath = entMan.SpawnEntity(Goliath, testMap.GridCoords);
        });

        await server.WaitAssertion(() =>
        {
            AssertRadii(entMan, goliath, 9f, 15f);
            entMan.System<ExpeditionMobSystem>().BoostMobVision(testMap.MapUid);
            AssertRadii(entMan, goliath, 9f, 15f);
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
