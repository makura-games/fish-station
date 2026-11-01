#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Shared._Fish.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Fish.Hands;

[TestFixture]
public sealed class HandOfferTest : GameTest
{
    private const string Item = "Crowbar";

    /// <summary>
    /// Протянутый предмет переходит из рук в руки через Alt-действия, а чужой не может его забрать.
    /// </summary>
    [Test]
    public async Task OfferedItemIsTakenByTarget()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();
            var verbs = entMan.System<SharedVerbSystem>();

            var giver = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var receiver = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var stranger = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var item = entMan.SpawnEntity(Item, testMap.GridCoords);

            Assert.That(hands.TryPickupAnyHand(giver, item));

            // Пока предмет не протянут, получателю нечего забирать.
            Assert.That(FindVerb(verbs, giver, receiver), Is.Null);

            var offer = FindVerb(verbs, receiver, giver);
            Assert.That(offer, Is.Not.Null, "Нет действия «протянуть» для предмета в руке");
            verbs.ExecuteVerb(offer!, giver, receiver);
            Assert.That(entMan.HasComponent<HandOfferComponent>(giver));

            // Третий персонаж забрать предмет не может.
            Assert.That(FindVerb(verbs, giver, stranger), Is.Null);

            var take = FindVerb(verbs, giver, receiver);
            Assert.That(take, Is.Not.Null, "Нет действия «взять» у получателя");
            verbs.ExecuteVerb(take!, receiver, giver);

            Assert.That(hands.IsHolding(receiver, item));
            Assert.That(hands.IsHolding(giver, item), Is.False);
            Assert.That(entMan.HasComponent<HandOfferComponent>(giver), Is.False);
        });
    }

    /// <summary>
    /// Если предмет убрали из рук после предложения, забрать его уже нельзя.
    /// </summary>
    [Test]
    public async Task OfferExpiresWhenItemLeavesHands()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();
            var verbs = entMan.System<SharedVerbSystem>();

            var giver = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var receiver = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var item = entMan.SpawnEntity(Item, testMap.GridCoords);

            Assert.That(hands.TryPickupAnyHand(giver, item));

            var offer = FindVerb(verbs, receiver, giver);
            Assert.That(offer, Is.Not.Null);
            verbs.ExecuteVerb(offer!, giver, receiver);

            Assert.That(hands.TryDrop(giver, item));
            Assert.That(FindVerb(verbs, giver, receiver), Is.Null);
            Assert.That(entMan.HasComponent<HandOfferComponent>(giver), Is.False,
                "Предложение осталось после того, как предмет покинул руки");
        });
    }

    /// <summary>
    /// Ищет Alt-действие передачи: <paramref name="target"/> — цель клика, <paramref name="user"/> — кликающий.
    /// </summary>
    private static AlternativeVerb? FindVerb(SharedVerbSystem verbs, EntityUid target, EntityUid user)
    {
        // Параметры идут в порядке (цель, кто кликает), как в GetLocalVerbs.
        foreach (var verb in verbs.GetLocalVerbs(target, user, typeof(AlternativeVerb)))
        {
            if (verb is AlternativeVerb alt && alt.Priority == 10)
                return alt;
        }

        return null;
    }
}
