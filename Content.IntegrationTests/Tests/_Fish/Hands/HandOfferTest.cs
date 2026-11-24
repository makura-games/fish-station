#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Server.Inventory;
using Content.Server.Silicons.Borgs;
using Content.Shared._Fish.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Silicons.Borgs.Components;
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
    /// Виртуальный предмет и предмет, ставший неудаляемым после предложения, не передаются.
    /// Обычный предмет по-прежнему уходит из рук в руки.
    /// </summary>
    [Test]
    public async Task VirtualAndUnremoveableItemsCannotBeHandedOff()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await Pair.CreateTestMap();

        EntityUid giver = default;
        EntityUid receiver = default;
        EntityUid virtualItem = default;
        EntityUid crowbar = default;
        var virtualOffered = true;
        var virtualTaken = true;
        var crowbarOffered = false;
        AlternativeVerb? virtualVerb = null;
        AlternativeVerb? takeAfterLock = null;

        await server.WaitPost(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();
            var verbs = entMan.System<SharedVerbSystem>();
            var offers = entMan.System<HandOfferSystem>();
            var virtualItems = entMan.System<VirtualItemSystem>();

            giver = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            receiver = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var blocker = entMan.SpawnEntity("Crowbar", testMap.GridCoords);

            Assert.That(virtualItems.TrySpawnVirtualItemInHand(blocker, giver, out var spawned), Is.True);
            virtualItem = spawned!.Value;
            Assert.That(hands.IsHolding(giver, virtualItem, out var virtualHand), Is.True);
            hands.TrySetActiveHand(giver, virtualHand);

            virtualVerb = FindVerb(verbs, receiver, giver);
            virtualOffered = offers.TryOffer(giver, receiver, virtualItem);
            virtualTaken = offers.DoTake(receiver, giver, virtualItem);

            crowbar = entMan.SpawnEntity("Crowbar", testMap.GridCoords);
            Assert.That(hands.TryPickupAnyHand(giver, crowbar), Is.True);
            Assert.That(hands.IsHolding(giver, crowbar, out var crowbarHand), Is.True);
            hands.TrySetActiveHand(giver, crowbarHand);

            crowbarOffered = offers.TryOffer(giver, receiver, crowbar);
            entMan.EnsureComponent<UnremoveableComponent>(crowbar);
            takeAfterLock = FindVerb(verbs, giver, receiver);
        });

        await server.WaitAssertion(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();
            var offers = entMan.System<HandOfferSystem>();

            Assert.That(virtualVerb, Is.Null);
            Assert.That(virtualOffered, Is.False);
            Assert.That(virtualTaken, Is.False);
            Assert.That(entMan.EntityExists(virtualItem), Is.True);
            Assert.That(hands.IsHolding(giver, virtualItem), Is.True);
            Assert.That(hands.IsHolding(receiver, virtualItem), Is.False);

            Assert.That(crowbarOffered, Is.True, "Обычный предмет должен протягиваться");
            Assert.That(takeAfterLock, Is.Null);
            Assert.That(offers.TryTake(receiver, giver), Is.False);
            Assert.That(hands.IsHolding(giver, crowbar), Is.True);
            Assert.That(hands.IsHolding(receiver, crowbar), Is.False);
        });
    }

    /// <summary>
    /// Борг не отдаёт предмет своего модуля, но обычный предмет из chassis-руки передаёт.
    /// </summary>
    [Test]
    public async Task BorgModuleItemCannotBeHandedOff()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await Pair.CreateTestMap();

        EntityUid borg = default;
        EntityUid receiver = default;
        EntityUid moduleItem = default;
        EntityUid crowbar = default;
        var moduleOffered = true;
        var moduleTaken = true;
        AlternativeVerb? moduleVerb = null;
        AlternativeVerb? crowbarOffer = null;
        AlternativeVerb? crowbarTake = null;

        await server.WaitPost(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();
            var verbs = entMan.System<SharedVerbSystem>();
            var offers = entMan.System<HandOfferSystem>();
            var borgs = entMan.System<BorgSystem>();

            borg = entMan.SpawnEntity("BorgChassisSelectable", testMap.GridCoords);
            receiver = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var chassis = entMan.GetComponent<BorgChassisComponent>(borg);

            borgs.SetActive((borg, chassis), true);
            var module = entMan.SpawnEntity("BorgModuleCable", testMap.GridCoords);
            borgs.InsertModule((borg, chassis), module);

            foreach (var held in hands.EnumerateHeld(borg))
            {
                if (entMan.HasComponent<UnremoveableComponent>(held))
                    continue;

                moduleItem = held;
                break;
            }

            Assert.That(moduleItem, Is.Not.EqualTo(default(EntityUid)),
                "У модуля кабелей нет выбрасываемого предмета в руке");
            Assert.That(hands.IsHolding(borg, moduleItem, out var moduleHand), Is.True);
            hands.TrySetActiveHand(borg, moduleHand);

            moduleVerb = FindVerb(verbs, receiver, borg);
            moduleOffered = offers.TryOffer(borg, receiver, moduleItem);
            moduleTaken = offers.DoTake(receiver, borg, moduleItem);

            hands.AddHand(borg, "hand_right", new Hand(HandLocation.Right));
            crowbar = entMan.SpawnEntity("Crowbar", testMap.GridCoords);
            Assert.That(hands.TryPickup(borg, crowbar, "hand_right"), Is.True);
            hands.TrySetActiveHand(borg, "hand_right");
            crowbarOffer = FindVerb(verbs, receiver, borg);
            if (crowbarOffer != null)
                verbs.ExecuteVerb(crowbarOffer, borg, receiver);

            crowbarTake = FindVerb(verbs, borg, receiver);
            if (crowbarTake != null)
                verbs.ExecuteVerb(crowbarTake, receiver, borg);
        });

        await server.WaitAssertion(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();

            Assert.That(moduleVerb, Is.Null);
            Assert.That(moduleOffered, Is.False);
            Assert.That(moduleTaken, Is.False);
            Assert.That(hands.IsHolding(borg, moduleItem), Is.True);
            Assert.That(hands.IsHolding(receiver, moduleItem), Is.False);

            Assert.That(crowbarOffer, Is.Not.Null, "Нет действия «протянуть» для предмета в chassis-руке");
            Assert.That(crowbarTake, Is.Not.Null, "Нет действия «взять» у получателя");
            Assert.That(hands.IsHolding(receiver, crowbar), Is.True);
            Assert.That(hands.IsHolding(borg, crowbar), Is.False);
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
