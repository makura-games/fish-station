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

        EntityUid giver = default;
        EntityUid receiver = default;
        EntityUid stranger = default;
        EntityUid item = default;
        AlternativeVerb? takeBeforeOffer = null;
        AlternativeVerb? offer = null;
        AlternativeVerb? strangerTake = null;
        AlternativeVerb? take = null;

        await server.WaitPost(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();
            var verbs = entMan.System<SharedVerbSystem>();

            giver = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            receiver = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            stranger = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            item = entMan.SpawnEntity(Item, testMap.GridCoords);

            hands.TryPickupAnyHand(giver, item);

            // Пока предмет не протянут, получателю нечего забирать.
            takeBeforeOffer = FindVerb(verbs, giver, receiver);

            offer = FindVerb(verbs, receiver, giver);
            if (offer != null)
                verbs.ExecuteVerb(offer, giver, receiver);

            // Третий персонаж забрать предмет не может.
            strangerTake = FindVerb(verbs, giver, stranger);

            take = FindVerb(verbs, giver, receiver);
            if (take != null)
                verbs.ExecuteVerb(take, receiver, giver);
        });

        await server.WaitAssertion(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();

            Assert.That(takeBeforeOffer, Is.Null);
            Assert.That(offer, Is.Not.Null, "Нет действия «протянуть» для предмета в руке");
            Assert.That(strangerTake, Is.Null);
            Assert.That(take, Is.Not.Null, "Нет действия «взять» у получателя");

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

        EntityUid giver = default;
        EntityUid receiver = default;
        AlternativeVerb? offer = null;
        var hasOfferAfterGive = false;

        await server.WaitPost(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();
            var verbs = entMan.System<SharedVerbSystem>();

            giver = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            receiver = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var item = entMan.SpawnEntity(Item, testMap.GridCoords);

            hands.TryPickupAnyHand(giver, item);

            offer = FindVerb(verbs, receiver, giver);
            if (offer != null)
                verbs.ExecuteVerb(offer, giver, receiver);

            hasOfferAfterGive = entMan.HasComponent<HandOfferComponent>(giver);

            hands.TryDrop(giver, item);
        });

        await server.WaitAssertion(() =>
        {
            var verbs = entMan.System<SharedVerbSystem>();

            Assert.That(offer, Is.Not.Null);
            Assert.That(hasOfferAfterGive, "Предложение не появилось после протягивания");
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
        foreach (var verb in verbs.GetLocalVerbs(target, user, typeof(AlternativeVerb)))
        {
            if (verb is AlternativeVerb alt && alt.Priority == HandOfferSystem.VerbPriority)
                return alt;
        }

        return null;
    }
}
