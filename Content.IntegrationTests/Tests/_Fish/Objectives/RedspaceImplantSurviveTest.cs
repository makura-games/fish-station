#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.Tests._Fish.Objectives;

[TestFixture]
public sealed class RedspaceImplantSurviveTest : GameTest
{
    private const string SurviveObjective = "NinjaSurviveObjective";
    private const string RedspaceImplant = "RedspaceImplant";

    /// <summary>
    /// Когда игрок сам активирует редспейс-имплант, тело удаляется, но цель «выжить» должна быть выполнена.
    /// </summary>
    [Test]
    public async Task ActivatingRedspaceImplantCompletesSurviveObjective()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await Pair.CreateTestMap();

        EntityUid body = default;
        Entity<MindComponent> mind = default;
        EntityUid objective = default;

        await server.WaitAssertion(() =>
        {
            (body, mind, objective, var implant) = Setup(entMan, testMap);
            var objectives = entMan.System<SharedObjectivesSystem>();

            // Пока персонаж жив, цель выполнена.
            Assert.That(objectives.GetProgress(objective, mind), Is.EqualTo(1f));

            var activate = new ActivateImplantEvent { Performer = body };
            entMan.EventBus.RaiseLocalEvent(implant, activate);
        });

        // Имплант ставит тело в очередь на удаление, само удаление происходит в конце тика.
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var objectives = entMan.System<SharedObjectivesSystem>();

            // Владелец удалён, но цель остаётся выполненной.
            Assert.That(entMan.Deleted(body), Is.True);
            Assert.That(objectives.GetProgress(objective, mind), Is.EqualTo(1f));
        });
    }

    /// <summary>
    /// Если тело исчезло без активации импланта, цель «выжить» по-прежнему не выполнена.
    /// </summary>
    [Test]
    public async Task DeletingBodyWithoutActivationFailsSurviveObjective()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var (body, mind, objective, _) = Setup(entMan, testMap);
            var objectives = entMan.System<SharedObjectivesSystem>();

            entMan.DeleteEntity(body);

            Assert.That(objectives.GetProgress(objective, mind), Is.EqualTo(0f));
        });
    }

    /// <summary>
    /// Создаёт человека с разумом, цель «выжить» и редспейс-имплант в теле.
    /// </summary>
    private static (EntityUid Body, Entity<MindComponent> Mind, EntityUid Objective, EntityUid Implant) Setup(
        IEntityManager entMan,
        TestMapData testMap)
    {
        var mindSystem = entMan.System<SharedMindSystem>();
        var implantSystem = entMan.System<SharedSubdermalImplantSystem>();

        var body = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
        var mind = mindSystem.CreateMind(null, "Redspace Tester");
        mindSystem.TransferTo(mind, body, mind: mind);

        var objective = entMan.SpawnEntity(SurviveObjective, MapCoordinates.Nullspace);

        var implant = implantSystem.AddImplant(body, RedspaceImplant);
        Assert.That(implant, Is.Not.Null);

        return (body, mind, objective, implant!.Value);
    }
}
