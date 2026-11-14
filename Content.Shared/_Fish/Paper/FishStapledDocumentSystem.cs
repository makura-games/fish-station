using System.Linq;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using static Content.Shared.Paper.PaperComponent;

namespace Content.Shared._Fish.Paper;

/// <summary>
/// Скрепление бумаг степлером в многостраничный пакет с навигацией и разборкой.
/// </summary>
public sealed partial class FishStapledDocumentSystem : EntitySystem
{
    private static readonly ProtoId<ItemSizePrototype> BundleItemSize = "Tiny";

    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private PaperSystem _paper = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishStapledDocumentComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<FishStapledDocumentComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
        SubscribeLocalEvent<FishStapledDocumentComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<FishStapledDocumentComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<FishStapledDocumentComponent, GetPaperContentTargetEvent>(OnGetContentTarget);
        SubscribeLocalEvent<FishStapledDocumentComponent, GetPaperPageInfoEvent>(OnGetPageInfo);
        SubscribeLocalEvent<FishStapledDocumentComponent, FishPaperPageTurnMessage>(OnPageTurn);
        SubscribeLocalEvent<FishStapledDocumentComponent, FishPaperRemoveCurrentPageMessage>(OnRemoveCurrentPageMessage);
    }

    private void OnInit(Entity<FishStapledDocumentComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Pages = _container.EnsureContainer<Container>(ent, FishStapledDocumentComponent.ContainerId);
        ent.Comp.Pages.ShowContents = false;
        Dirty(ent);
    }

    private void OnEntRemoved(Entity<FishStapledDocumentComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != FishStapledDocumentComponent.ContainerId)
            return;

        if (ent.Comp.Pages.Count == 0)
        {
            ent.Comp.CurrentPage = 0;
            RemComp<FishStapledDocumentComponent>(ent);
            RefreshUi(ent.Owner); // Fish-edit - сбрасываем UI пакета после снятия компонента
            return;
        }

        ClampPage(ent);
        Dirty(ent);
        UpdateBundleDescription(ent);
        RefreshUi(ent);
    }

    private void OnExamined(Entity<FishStapledDocumentComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("fish-stapled-document-examine", ("pages", GetPageCount(ent))));
    }

    private void OnGetContentTarget(Entity<FishStapledDocumentComponent> ent, ref GetPaperContentTargetEvent args)
    {
        if (TryGetPageEntity(ent, ent.Comp.CurrentPage, out var page))
            args.Target = page;
    }

    private void OnGetPageInfo(Entity<FishStapledDocumentComponent> ent, ref GetPaperPageInfoEvent args)
    {
        args.PageIndex = ent.Comp.CurrentPage;
        args.PageCount = GetPageCount(ent);
    }

    private void OnPageTurn(Entity<FishStapledDocumentComponent> ent, ref FishPaperPageTurnMessage args)
    {
        TryPageTurn(ent, args.Actor, args.Delta);
    }

    private bool CanPageTurn(Entity<FishStapledDocumentComponent> ent, int delta, out PaperComponent? paper, out int nextPage)
    {
        paper = null;
        nextPage = ent.Comp.CurrentPage;
        if (delta == 0)
            return false;

        // Нельзя листать во время записи — иначе текст уйдёт на другую страницу.
        if (TryComp(ent, out paper) && paper.Mode == PaperAction.Write)
            return false;

        var count = GetPageCount(ent);
        if (count <= 1)
            return false;

        nextPage = Math.Clamp(ent.Comp.CurrentPage + delta, 0, count - 1);
        return nextPage != ent.Comp.CurrentPage;
    }

    private bool TryPageTurn(Entity<FishStapledDocumentComponent> ent, EntityUid actor, int delta)
    {
        if (!CanPageTurn(ent, delta, out var paper, out var nextPage))
            return false;

        ent.Comp.CurrentPage = nextPage;
        Dirty(ent);
        RefreshUi(ent);

        if (paper?.Sound != null)
            _audio.PlayPredicted(paper.Sound, ent, actor);

        return true;
    }

    private void OnRemoveCurrentPageMessage(Entity<FishStapledDocumentComponent> ent, ref FishPaperRemoveCurrentPageMessage args)
    {
        TryRemoveCurrentPage(ent, args.Actor, quiet: false);
    }

    private void OnGetAltVerbs(Entity<FishStapledDocumentComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("fish-stapled-document-verb-remove-page"),
            Act = () => TryRemoveCurrentPage(ent, user, quiet: false),
            Priority = 1,
        });

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("fish-stapled-document-verb-separate-all"),
            Act = () => TrySeparateAll(ent, user),
            Priority = 0,
        });
    }

    /// <summary>
    /// Вызывается из PaperSystem при InteractUsing: бумага по бумаге со степлером в другой руке.
    /// </summary>
    public bool TryHandlePaperInteractUsing(Entity<PaperComponent> target, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return false;

        if (!HasComp<PaperComponent>(args.Used))
            return false;

        if (args.Used == target.Owner)
            return false;

        if (!TryGetHeldStapler(args.User, args.Used, out var stapler))
            return false;

        if (_net.IsClient)
        {
            args.Handled = true;
            return true;
        }

        if (TryStaple(args.User, args.Used, target.Owner, stapler))
        {
            args.Handled = true;
            return true;
        }

        // Степлер есть, но скрепление не удалось — не даём пройти в stamp/write логику бумаги.
        args.Handled = true;
        return true;
    }

    /// <summary>
    /// Скрепляет <paramref name="toAppend"/> к пакету с внешней страницей <paramref name="outer"/>.
    /// </summary>
    public bool TryStaple(EntityUid user, EntityUid outer, EntityUid toAppend, Entity<FishStaplerComponent> stapler)
    {
        if (!CanStaple(user, outer, toAppend, quiet: false))
            return false;

        EnsureBundle(outer, out var outerBundle);

        // Считаем страницы до разборки toAppend — иначе при отказе по лимиту пакет уже разрушен.
        var pagesToAddCount = CountPages(toAppend);
        var free = outerBundle.Comp.MaxPages - GetPageCount(outerBundle);
        if (pagesToAddCount > free)
        {
            _popup.PopupEntity(Loc.GetString("fish-stapled-document-too-many", ("max", outerBundle.Comp.MaxPages)), outer, user);
            return false;
        }

        var pagesToAdd = CollectPagesInOrder(toAppend);
        foreach (var page in pagesToAdd)
        {
            if (page == outer)
                continue;

            if (!_container.Insert(page, outerBundle.Comp.Pages))
            {
                _popup.PopupEntity(Loc.GetString("fish-stapled-document-insert-failed"), outer, user);
                return false;
            }
        }

        Dirty(outerBundle);
        EnsureSingleHandBundle(outer);
        UpdateBundleDescription(outerBundle);
        RefreshUi(outerBundle);

        _audio.PlayPredicted(stapler.Comp.StapleSound, outer, user);
        _popup.PopupEntity(
            Loc.GetString("fish-stapled-document-stapled", ("pages", GetPageCount(outerBundle))),
            outer,
            user);

        return true;
    }

    public bool CanStaple(EntityUid user, EntityUid outer, EntityUid toAppend, bool quiet = false)
    {
        if (!Exists(outer) || !Exists(toAppend) || outer == toAppend)
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("fish-stapled-document-invalid"), outer, user);
            return false;
        }

        if (!HasComp<PaperComponent>(outer) || !HasComp<PaperComponent>(toAppend))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("fish-stapled-document-unsupported"), outer, user);
            return false;
        }

        if (_container.TryGetContainingContainer((toAppend, null), out var c1) && c1.Owner == outer)
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("fish-stapled-document-already-inside"), outer, user);
            return false;
        }

        if (_container.TryGetContainingContainer((outer, null), out var c2) && c2.Owner == toAppend)
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("fish-stapled-document-already-inside"), outer, user);
            return false;
        }

        if (!TryGetHeldStapler(user, outer, out _))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("fish-stapled-document-need-stapler"), outer, user);
            return false;
        }

        return true;
    }

    public bool TryRemoveCurrentPage(Entity<FishStapledDocumentComponent> ent, EntityUid user, bool quiet)
    {
        if (_net.IsClient)
            return false;

        // Нельзя отсоединять страницу во время записи — иначе ContentTarget уйдёт мимо пакета.
        if (TryComp(ent, out PaperComponent? writingPaper) && writingPaper.Mode == PaperAction.Write)
            return false;

        var count = GetPageCount(ent);
        if (count <= 1)
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("fish-stapled-document-single-page"), ent, user);
            return false;
        }

        // Внешнюю страницу (индекс 0) нельзя извлечь — снимаем текущую внутреннюю
        // либо последнюю, если открыта внешняя.
        var removeIndex = ent.Comp.CurrentPage == 0 ? count - 1 : ent.Comp.CurrentPage;
        if (!TryGetPageEntity(ent, removeIndex, out var removed) || removeIndex == 0)
            return false;

        if (!_container.Remove(removed, ent.Comp.Pages))
            return false;

        _hands.PickupOrDrop(user, removed);

        if (ent.Comp.Pages.Count == 0)
        {
            RemComp<FishStapledDocumentComponent>(ent);
            RefreshUi(ent.Owner);
        }
        else
        {
            ClampPage(ent);
            Dirty(ent);
            UpdateBundleDescription(ent);
            RefreshUi(ent);
        }

        if (!quiet)
            _popup.PopupEntity(Loc.GetString("fish-stapled-document-page-removed"), ent, user);

        return true;
    }

    public bool TrySeparateAll(Entity<FishStapledDocumentComponent> ent, EntityUid user)
    {
        if (_net.IsClient)
            return false;

        // Нельзя разбирать пакет во время записи.
        if (TryComp(ent, out PaperComponent? writingPaper) && writingPaper.Mode == PaperAction.Write)
            return false;

        var pages = ent.Comp.Pages.ContainedEntities.ToList();
        foreach (var page in pages)
        {
            _container.Remove(page, ent.Comp.Pages);
            _hands.PickupOrDrop(user, page);
        }

        RemComp<FishStapledDocumentComponent>(ent);
        RefreshUi(ent.Owner);
        _popup.PopupEntity(Loc.GetString("fish-stapled-document-separated"), ent, user);
        return true;
    }

    public int GetPageCount(Entity<FishStapledDocumentComponent> ent)
    {
        return 1 + ent.Comp.Pages.Count;
    }

    /// <summary>
    /// Число страниц без изменения сущностей (для проверки лимита до CollectPagesInOrder).
    /// </summary>
    public int CountPages(EntityUid entity)
    {
        if (TryComp<FishStapledDocumentComponent>(entity, out var stapled))
            return GetPageCount((entity, stapled));

        return HasComp<PaperComponent>(entity) ? 1 : 0;
    }

    public bool TryGetPageEntity(Entity<FishStapledDocumentComponent> ent, int index, out EntityUid page)
    {
        if (index < 0)
        {
            page = default;
            return false;
        }

        if (index == 0)
        {
            page = ent.Owner;
            return true;
        }

        var innerIndex = index - 1;
        if (innerIndex >= ent.Comp.Pages.Count)
        {
            page = default;
            return false;
        }

        page = ent.Comp.Pages.ContainedEntities[innerIndex];
        return true;
    }

    private void EnsureBundle(EntityUid outer, out Entity<FishStapledDocumentComponent> bundle)
    {
        var comp = EnsureComp<FishStapledDocumentComponent>(outer);
        comp.Pages = _container.EnsureContainer<Container>(outer, FishStapledDocumentComponent.ContainerId);
        comp.Pages.ShowContents = false;
        bundle = (outer, comp);
    }

    /// <summary>
    /// Собирает сущность и все её внутренние страницы по порядку.
    /// У уже скреплённого пакета снимает компонент после извлечения страниц.
    /// </summary>
    private List<EntityUid> CollectPagesInOrder(EntityUid entity)
    {
        var result = new List<EntityUid> { entity };

        if (!TryComp<FishStapledDocumentComponent>(entity, out var stapled))
            return result;

        var inner = stapled.Pages.ContainedEntities.ToList();
        foreach (var page in inner)
        {
            _container.Remove(page, stapled.Pages);
            result.Add(page);
        }

        RemComp<FishStapledDocumentComponent>(entity);
        return result;
    }

    private bool TryGetHeldStapler(EntityUid user, EntityUid exceptItem, out Entity<FishStaplerComponent> stapler)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (held == exceptItem)
                continue;

            if (TryComp<FishStaplerComponent>(held, out var staplerComp))
            {
                stapler = (held, staplerComp);
                return true;
            }
        }

        stapler = default;
        return false;
    }

    private void ClampPage(Entity<FishStapledDocumentComponent> ent)
    {
        var max = Math.Max(0, GetPageCount(ent) - 1);
        ent.Comp.CurrentPage = Math.Clamp(ent.Comp.CurrentPage, 0, max);
    }

    private void RefreshUi(EntityUid uid)
    {
        if (TryComp<PaperComponent>(uid, out var paper))
            _paper.RefreshUi((uid, paper));
    }

    private void UpdateBundleDescription(Entity<FishStapledDocumentComponent> ent)
    {
        if (!TryComp(ent, out MetaDataComponent? meta))
            return;

        _meta.SetEntityDescription(ent,
            Loc.GetString("fish-stapled-document-desc", ("pages", GetPageCount(ent))),
            meta);
    }

    /// <summary>
    /// Пакет всегда один предмет в одной руке: Tiny, без MultiHanded.
    /// Внутренние страницы уже в контейнере и не занимают отдельные слоты рук.
    /// </summary>
    private void EnsureSingleHandBundle(EntityUid bundle)
    {
        RemComp<MultiHandedItemComponent>(bundle);

        if (TryComp<ItemComponent>(bundle, out var item))
            _item.SetSize(bundle, BundleItemSize, item);
    }
}
