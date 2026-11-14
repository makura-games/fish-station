#nullable enable
using Content.Shared._Fish.Paper;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared.Paper;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.Paper;

[TestFixture]
[TestOf(typeof(FishStapledDocumentSystem))]
public sealed class FishStapledDocumentTest
{
    [Test]
    public async Task StapleTwoPapers_PreservesOrderTextAndStamps()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var stapleSys = entMan.System<FishStapledDocumentSystem>();
        var paperSys = entMan.System<PaperSystem>();
        var hands = entMan.System<SharedHandsSystem>();

        EntityUid user = default;
        EntityUid outer = default;
        EntityUid inner = default;
        EntityUid stapler = default;

        await server.WaitPost(() =>
        {
            user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            EnsureTwoHands(entMan, hands, user);

            outer = entMan.SpawnEntity("Paper", map.GridCoords);
            inner = entMan.SpawnEntity("Paper", map.GridCoords);
            stapler = entMan.SpawnEntity("FishStapler", map.GridCoords);

            paperSys.SetContent(outer, "OUTER_PAGE_TEXT");
            paperSys.SetContent(inner, "INNER_PAGE_TEXT");
            paperSys.TryStamp((outer, entMan.GetComponent<PaperComponent>(outer)),
                new StampDisplayInfo { StampedName = "stamp-component-stamped-name-default", StampedColor = Color.Red },
                "paper_stamp-generic");
            paperSys.TryStamp((inner, entMan.GetComponent<PaperComponent>(inner)),
                new StampDisplayInfo { StampedName = "stamp-component-stamped-name-centcom", StampedColor = Color.Blue },
                "paper_stamp-centcom");

            Assert.That(hands.TryPickupAnyHand(user, stapler, checkActionBlocker: false), Is.True);
            Assert.That(hands.TryPickupAnyHand(user, outer, checkActionBlocker: false), Is.True);
        });

        var stapled = false;
        await server.WaitPost(() =>
        {
            stapled = stapleSys.TryStaple(user, outer, inner, (stapler, entMan.GetComponent<FishStaplerComponent>(stapler)));
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(stapled, Is.True);
            Assert.That(entMan.TryGetComponent(outer, out FishStapledDocumentComponent? bundleComp), Is.True);
            var bundle = (outer, bundleComp!);
            Assert.That(stapleSys.GetPageCount(bundle), Is.EqualTo(2));

            Assert.That(stapleSys.TryGetPageEntity(bundle, 0, out var page0), Is.True);
            Assert.That(page0, Is.EqualTo(outer));
            Assert.That(stapleSys.TryGetPageEntity(bundle, 1, out var page1), Is.True);
            Assert.That(page1, Is.EqualTo(inner));

            var outerPaper = entMan.GetComponent<PaperComponent>(outer);
            var innerPaper = entMan.GetComponent<PaperComponent>(inner);
            Assert.That(outerPaper.Content, Is.EqualTo("OUTER_PAGE_TEXT"));
            Assert.That(innerPaper.Content, Is.EqualTo("INNER_PAGE_TEXT"));
            Assert.That(outerPaper.StampedBy, Has.Count.EqualTo(1));
            Assert.That(innerPaper.StampedBy, Has.Count.EqualTo(1));
            Assert.That(outerPaper.StampState, Is.EqualTo("paper_stamp-generic"));
            Assert.That(innerPaper.StampState, Is.EqualTo("paper_stamp-centcom"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StapleMultipleAndCombineBundles_PreservesOrder()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var stapleSys = entMan.System<FishStapledDocumentSystem>();
        var paperSys = entMan.System<PaperSystem>();
        var hands = entMan.System<SharedHandsSystem>();

        EntityUid user = default;
        EntityUid a = default;
        EntityUid b = default;
        EntityUid c = default;
        EntityUid d = default;
        EntityUid stapler = default;

        await server.WaitPost(() =>
        {
            user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            EnsureTwoHands(entMan, hands, user);
            a = entMan.SpawnEntity("Paper", map.GridCoords);
            b = entMan.SpawnEntity("Paper", map.GridCoords);
            c = entMan.SpawnEntity("Paper", map.GridCoords);
            d = entMan.SpawnEntity("Paper", map.GridCoords);
            stapler = entMan.SpawnEntity("FishStapler", map.GridCoords);
            paperSys.SetContent(a, "A");
            paperSys.SetContent(b, "B");
            paperSys.SetContent(c, "C");
            paperSys.SetContent(d, "D");
            Assert.That(hands.TryPickupAnyHand(user, stapler, checkActionBlocker: false), Is.True);
            Assert.That(hands.TryPickupAnyHand(user, a, checkActionBlocker: false), Is.True);
        });

        await server.WaitPost(() =>
        {
            var staplerEnt = (stapler, entMan.GetComponent<FishStaplerComponent>(stapler));
            Assert.That(stapleSys.TryStaple(user, a, b, staplerEnt), Is.True);
            // Держим оба пакета: a уже в руке, c подбираем на свободную после отпускания stapler не нужно —
            // степлер должен оставаться в другой руке.
            Assert.That(stapleSys.TryStaple(user, c, d, staplerEnt), Is.True);
            Assert.That(stapleSys.TryStaple(user, a, c, staplerEnt), Is.True);
        });

        await server.WaitAssertion(() =>
        {
            var bundle = entMan.GetComponent<FishStapledDocumentComponent>(a);
            Assert.That(stapleSys.GetPageCount((a, bundle)), Is.EqualTo(4));
            Assert.That(entMan.HasComponent<FishStapledDocumentComponent>(c), Is.False);

            Assert.Multiple(() =>
            {
                Assert.That(GetPageContent(entMan, stapleSys, a, 0), Is.EqualTo("A"));
                Assert.That(GetPageContent(entMan, stapleSys, a, 1), Is.EqualTo("B"));
                Assert.That(GetPageContent(entMan, stapleSys, a, 2), Is.EqualTo("C"));
                Assert.That(GetPageContent(entMan, stapleSys, a, 3), Is.EqualTo("D"));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PageNavigationAndSeparation_NoDuplicationOrLoss()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var stapleSys = entMan.System<FishStapledDocumentSystem>();
        var paperSys = entMan.System<PaperSystem>();
        var hands = entMan.System<SharedHandsSystem>();

        EntityUid user = default;
        EntityUid outer = default;
        EntityUid p2 = default;
        EntityUid p3 = default;
        EntityUid stapler = default;

        await server.WaitPost(() =>
        {
            user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            EnsureTwoHands(entMan, hands, user);
            outer = entMan.SpawnEntity("Paper", map.GridCoords);
            p2 = entMan.SpawnEntity("Paper", map.GridCoords);
            p3 = entMan.SpawnEntity("Paper", map.GridCoords);
            stapler = entMan.SpawnEntity("FishStapler", map.GridCoords);
            paperSys.SetContent(outer, "P1");
            paperSys.SetContent(p2, "P2");
            paperSys.SetContent(p3, "P3");
            Assert.That(hands.TryPickupAnyHand(user, stapler, checkActionBlocker: false), Is.True);
            Assert.That(hands.TryPickupAnyHand(user, outer, checkActionBlocker: false), Is.True);
            var staplerEnt = (stapler, entMan.GetComponent<FishStaplerComponent>(stapler));
            Assert.That(stapleSys.TryStaple(user, outer, p2, staplerEnt), Is.True);
            Assert.That(stapleSys.TryStaple(user, outer, p3, staplerEnt), Is.True);
        });

        await server.WaitPost(() =>
        {
            var bundle = entMan.GetComponent<FishStapledDocumentComponent>(outer);
            bundle.CurrentPage = 1;
            Dirty(entMan, (outer, bundle));

            var targetEv = new GetPaperContentTargetEvent(outer);
            entMan.EventBus.RaiseLocalEvent(outer, ref targetEv);
            Assert.That(targetEv.Target, Is.EqualTo(p2));

            Assert.That(stapleSys.TryRemoveCurrentPage((outer, bundle), user, quiet: true), Is.True);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(p2), Is.True);
            Assert.That(entMan.EntityExists(p3), Is.True);
            Assert.That(entMan.EntityExists(outer), Is.True);

            var bundle = entMan.GetComponent<FishStapledDocumentComponent>(outer);
            Assert.That(stapleSys.GetPageCount((outer, bundle)), Is.EqualTo(2));
            Assert.That(GetPageContent(entMan, stapleSys, outer, 0), Is.EqualTo("P1"));
            Assert.That(GetPageContent(entMan, stapleSys, outer, 1), Is.EqualTo("P3"));
            Assert.That(entMan.GetComponent<PaperComponent>(p2).Content, Is.EqualTo("P2"));
        });

        await server.WaitPost(() =>
        {
            var bundle = entMan.GetComponent<FishStapledDocumentComponent>(outer);
            Assert.That(stapleSys.TrySeparateAll((outer, bundle), user), Is.True);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<FishStapledDocumentComponent>(outer), Is.False);
            Assert.That(entMan.EntityExists(outer), Is.True);
            Assert.That(entMan.EntityExists(p3), Is.True);
            Assert.That(entMan.GetComponent<PaperComponent>(outer).Content, Is.EqualTo("P1"));
            Assert.That(entMan.GetComponent<PaperComponent>(p3).Content, Is.EqualTo("P3"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StapleOverLimit_DoesNotDismantleSourceBundle()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var stapleSys = entMan.System<FishStapledDocumentSystem>();
        var hands = entMan.System<SharedHandsSystem>();

        await server.WaitPost(() =>
        {
            var user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            EnsureTwoHands(entMan, hands, user);
            var outer = entMan.SpawnEntity("Paper", map.GridCoords);
            var filler = entMan.SpawnEntity("Paper", map.GridCoords);
            var stapler = entMan.SpawnEntity("FishStapler", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(user, stapler, checkActionBlocker: false), Is.True);
            Assert.That(hands.TryPickupAnyHand(user, outer, checkActionBlocker: false), Is.True);

            var staplerEnt = (stapler, entMan.GetComponent<FishStaplerComponent>(stapler));
            Assert.That(stapleSys.TryStaple(user, outer, filler, staplerEnt), Is.True);

            // outer: 2 страницы, MaxPages=3 → свободен 1 слот.
            var bundle = entMan.GetComponent<FishStapledDocumentComponent>(outer);
            bundle.MaxPages = 3;
            Dirty(entMan, (outer, bundle));

            var source = entMan.SpawnEntity("Paper", map.GridCoords);
            var sourcePage = entMan.SpawnEntity("Paper", map.GridCoords);
            Assert.That(stapleSys.TryStaple(user, source, sourcePage, staplerEnt), Is.True);
            Assert.That(stapleSys.GetPageCount((source, entMan.GetComponent<FishStapledDocumentComponent>(source))), Is.EqualTo(2));

            // Добавление 2 страниц в пакет со свободным слотом 1 должно быть отклонено без разборки source.
            Assert.That(stapleSys.TryStaple(user, outer, source, staplerEnt), Is.False);
            Assert.That(entMan.HasComponent<FishStapledDocumentComponent>(source), Is.True);
            Assert.That(stapleSys.GetPageCount((source, entMan.GetComponent<FishStapledDocumentComponent>(source))), Is.EqualTo(2));
            Assert.That(entMan.EntityExists(sourcePage), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StapledBundle_OccupiesSingleHandSlot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var stapleSys = entMan.System<FishStapledDocumentSystem>();
        var hands = entMan.System<SharedHandsSystem>();

        EntityUid user = default;
        EntityUid outer = default;
        EntityUid inner = default;
        EntityUid stapler = default;

        await server.WaitPost(() =>
        {
            user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            EnsureTwoHands(entMan, hands, user);
            outer = entMan.SpawnEntity("Paper", map.GridCoords);
            inner = entMan.SpawnEntity("Paper", map.GridCoords);
            stapler = entMan.SpawnEntity("FishStapler", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(user, stapler, checkActionBlocker: false), Is.True);
            Assert.That(hands.TryPickupAnyHand(user, outer, checkActionBlocker: false), Is.True);
            var staplerEnt = (stapler, entMan.GetComponent<FishStaplerComponent>(stapler));
            Assert.That(stapleSys.TryStaple(user, outer, inner, staplerEnt), Is.True);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<MultiHandedItemComponent>(outer), Is.False);
            Assert.That(entMan.GetComponent<ItemComponent>(outer).Size, Is.EqualTo(new ProtoId<ItemSizePrototype>("Tiny")));

            Assert.That(hands.IsHolding(user, outer), Is.True);
            Assert.That(hands.IsHolding(user, inner), Is.False);

            var heldPapers = 0;
            foreach (var held in hands.EnumerateHeld(user))
            {
                if (entMan.HasComponent<PaperComponent>(held))
                    heldPapers++;
            }

            Assert.That(heldPapers, Is.EqualTo(1), "Скреплённый пакет должен занимать ровно один слот руки.");
            Assert.That(entMan.GetComponent<FishStapledDocumentComponent>(outer).Pages.Contains(inner), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SelfStapleAndMissingStapler_Rejected()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var stapleSys = entMan.System<FishStapledDocumentSystem>();
        var hands = entMan.System<SharedHandsSystem>();

        EntityUid user = default;
        EntityUid paper = default;
        EntityUid other = default;
        EntityUid stapler = default;

        await server.WaitPost(() =>
        {
            user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            EnsureTwoHands(entMan, hands, user);
            paper = entMan.SpawnEntity("Paper", map.GridCoords);
            other = entMan.SpawnEntity("Paper", map.GridCoords);
            stapler = entMan.SpawnEntity("FishStapler", map.GridCoords);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(stapleSys.CanStaple(user, paper, paper, quiet: true), Is.False);
            Assert.That(stapleSys.CanStaple(user, paper, other, quiet: true), Is.False);

            Assert.That(hands.TryPickupAnyHand(user, stapler, checkActionBlocker: false), Is.True);
            Assert.That(hands.TryPickupAnyHand(user, paper, checkActionBlocker: false), Is.True);
            Assert.That(stapleSys.CanStaple(user, paper, other, quiet: true), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WriteWithDetachedContentTarget_IsRejected()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var stapleSys = entMan.System<FishStapledDocumentSystem>();
        var paperSys = entMan.System<PaperSystem>();
        var hands = entMan.System<SharedHandsSystem>();

        EntityUid user = default;
        EntityUid outer = default;
        EntityUid inner = default;
        EntityUid stapler = default;

        await server.WaitPost(() =>
        {
            user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            EnsureTwoHands(entMan, hands, user);
            outer = entMan.SpawnEntity("Paper", map.GridCoords);
            inner = entMan.SpawnEntity("Paper", map.GridCoords);
            stapler = entMan.SpawnEntity("FishStapler", map.GridCoords);
            paperSys.SetContent(outer, "OUTER");
            paperSys.SetContent(inner, "INNER");
            Assert.That(hands.TryPickupAnyHand(user, stapler, checkActionBlocker: false), Is.True);
            Assert.That(hands.TryPickupAnyHand(user, outer, checkActionBlocker: false), Is.True);
            var staplerEnt = (stapler, entMan.GetComponent<FishStaplerComponent>(stapler));
            Assert.That(stapleSys.TryStaple(user, outer, inner, staplerEnt), Is.True);

            var bundle = entMan.GetComponent<FishStapledDocumentComponent>(outer);
            bundle.CurrentPage = 1;
            Dirty(entMan, (outer, bundle));
            Assert.That(stapleSys.TryRemoveCurrentPage((outer, bundle), user, quiet: true), Is.True);
            Assert.That(entMan.HasComponent<FishStapledDocumentComponent>(outer), Is.False);

            // Отклонённый ContentTarget возвращается до проверки ручки — ручка не нужна.
            var detachedNet = entMan.GetNetEntity(inner);
            var msg = new PaperComponent.PaperInputTextMessage("HACKED", detachedNet) { Actor = user };
            entMan.EventBus.RaiseLocalEvent(outer, msg);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<PaperComponent>(outer).Content, Is.EqualTo("OUTER"));
            Assert.That(entMan.GetComponent<PaperComponent>(inner).Content, Is.EqualTo("INNER"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovePage_BlockedWhileWriting()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var stapleSys = entMan.System<FishStapledDocumentSystem>();
        var hands = entMan.System<SharedHandsSystem>();

        await server.WaitPost(() =>
        {
            var user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            EnsureTwoHands(entMan, hands, user);
            var outer = entMan.SpawnEntity("Paper", map.GridCoords);
            var inner = entMan.SpawnEntity("Paper", map.GridCoords);
            var stapler = entMan.SpawnEntity("FishStapler", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(user, stapler, checkActionBlocker: false), Is.True);
            Assert.That(hands.TryPickupAnyHand(user, outer, checkActionBlocker: false), Is.True);
            var staplerEnt = (stapler, entMan.GetComponent<FishStaplerComponent>(stapler));
            Assert.That(stapleSys.TryStaple(user, outer, inner, staplerEnt), Is.True);

            var paper = entMan.GetComponent<PaperComponent>(outer);
            paper.Mode = PaperComponent.PaperAction.Write;
            entMan.Dirty(outer, paper);

            var bundle = entMan.GetComponent<FishStapledDocumentComponent>(outer);
            Assert.That(stapleSys.TryRemoveCurrentPage((outer, bundle), user, quiet: true), Is.False);
            Assert.That(stapleSys.TrySeparateAll((outer, bundle), user), Is.False);
            Assert.That(entMan.GetComponent<FishStapledDocumentComponent>(outer).Pages.Contains(inner), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TemplateSignatureAndJob_WriteOnCurrentInnerPage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var stapleSys = entMan.System<FishStapledDocumentSystem>();
        var paperSys = entMan.System<PaperSystem>();
        var hands = entMan.System<SharedHandsSystem>();

        EntityUid user = default;
        EntityUid outer = default;
        EntityUid inner = default;
        EntityUid pen = default;

        await server.WaitPost(() =>
        {
            user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            EnsureTwoHands(entMan, hands, user);
            outer = entMan.SpawnEntity("Paper", map.GridCoords);
            inner = entMan.SpawnEntity("Paper", map.GridCoords);
            var stapler = entMan.SpawnEntity("FishStapler", map.GridCoords);
            pen = entMan.SpawnEntity("Pen", map.GridCoords);
            paperSys.SetContent(outer, "OUTER [signature] [job]");
            paperSys.SetContent(inner, "INNER [signature] [job]");
            Assert.That(hands.TryPickupAnyHand(user, stapler, checkActionBlocker: false), Is.True);
            Assert.That(hands.TryPickupAnyHand(user, outer, checkActionBlocker: false), Is.True);
            var staplerEnt = (stapler, entMan.GetComponent<FishStaplerComponent>(stapler));
            Assert.That(stapleSys.TryStaple(user, outer, inner, staplerEnt), Is.True);

            var bundle = entMan.GetComponent<FishStapledDocumentComponent>(outer);
            bundle.CurrentPage = 1;
            Dirty(entMan, (outer, bundle));

            hands.TryDrop(user, stapler, checkActionBlocker: false);
            Assert.That(hands.TryPickupAnyHand(user, pen, checkActionBlocker: false), Is.True);
            Assert.That(hands.IsHolding(user, pen, out var penHand), Is.True);
            // TrySetActiveHand возвращает false, если рука уже активна.
            hands.SetActiveHand(user, penHand);
            Assert.That(hands.GetActiveItem(user), Is.EqualTo(pen));

            var sigMsg = new PaperComponent.PaperTemplateRequestMessage(
                PaperComponent.PaperTemplateRequestType.Signature, 0) { Actor = user };
            entMan.EventBus.RaiseLocalEvent(outer, sigMsg);

            var jobMsg = new PaperComponent.PaperTemplateRequestMessage(
                PaperComponent.PaperTemplateRequestType.Job, 0) { Actor = user };
            entMan.EventBus.RaiseLocalEvent(outer, jobMsg);
        });

        await server.WaitAssertion(() =>
        {
            var outerContent = entMan.GetComponent<PaperComponent>(outer).Content;
            var innerContent = entMan.GetComponent<PaperComponent>(inner).Content;
            var userName = entMan.GetComponent<MetaDataComponent>(user).EntityName;

            Assert.That(outerContent, Is.EqualTo("OUTER [signature] [job]"));
            Assert.That(innerContent, Does.Contain(userName));
            Assert.That(innerContent, Does.Not.Contain("[signature]"));
            Assert.That(innerContent, Does.Not.Contain("[job]"));
            Assert.That(innerContent, Does.StartWith("INNER "));
        });

        await pair.CleanReturnAsync();
    }

    private static void EnsureTwoHands(IEntityManager entMan, SharedHandsSystem hands, EntityUid user)
    {
        var handsComp = entMan.EnsureComponent<HandsComponent>(user);
        // MobHuman уже имеет две руки; добираем только если рук меньше двух.
        var i = 0;
        while (handsComp.SortedHands.Count < 2)
        {
            hands.AddHand(user, $"fish-test-hand-{i}", i == 0 ? HandLocation.Left : HandLocation.Right);
            i++;
        }
    }

    private static string GetPageContent(
        IEntityManager entMan,
        FishStapledDocumentSystem stapleSys,
        EntityUid bundle,
        int index)
    {
        var comp = entMan.GetComponent<FishStapledDocumentComponent>(bundle);
        Assert.That(stapleSys.TryGetPageEntity((bundle, comp), index, out var page), Is.True);
        return entMan.GetComponent<PaperComponent>(page).Content;
    }

    private static void Dirty(IEntityManager entMan, Entity<FishStapledDocumentComponent> ent)
    {
        entMan.Dirty(ent);
    }
}
