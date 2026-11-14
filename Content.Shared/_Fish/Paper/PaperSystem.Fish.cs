using System.Linq;
using Content.Shared._Sunrise.Paperwork;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Paper;

/// <summary>
/// Fish-хуки PaperSystem для степлерных пакетов документов.
/// </summary>
public sealed partial class PaperSystem
{
    /// <summary>
    /// Публичное обновление UI бумаги (например после смены страницы пакета).
    /// </summary>
    public void RefreshUi(Entity<PaperComponent> entity)
    {
        UpdateUserInterface(entity);
    }

    /// <summary>
    /// Целевая страница для чтения/записи/печати (внешний лист или текущая страница пакета).
    /// </summary>
    private EntityUid ResolvePaperContentTarget(EntityUid paper)
    {
        var ev = new GetPaperContentTargetEvent(paper);
        RaiseLocalEvent(paper, ref ev);
        return ev.Target;
    }

    /// <summary>
    /// Проверяет, что <paramref name="page"/> — сам пакет или страница внутри него.
    /// </summary>
    private bool IsValidPaperContentTarget(EntityUid bundle, EntityUid page)
    {
        if (page == bundle)
            return true;

        if (!TryComp<Content.Shared._Fish.Paper.FishStapledDocumentComponent>(bundle, out var stapled))
            return false;

        return stapled.Pages.ContainedEntities.Contains(page);
    }

    /// <summary>
    /// Подпись/должность пишутся на текущую страницу пакета; UI обновляется у внешнего листа.
    /// </summary>
    private void OnTemplateRequest(Entity<PaperComponent> entity, ref PaperComponent.PaperTemplateRequestMessage args)
    {
        var contentTarget = ResolvePaperContentTarget(entity);
        if (!_paperQuery.TryComp(contentTarget, out var contentPaper))
            return;
        var contentEntity = new Entity<PaperComponent>(contentTarget, contentPaper);

        switch (args.Type)
        {
            case PaperComponent.PaperTemplateRequestType.Signature:
                HandleSignatureRequest(contentEntity, args.Actor, args.Index);
                break;
            case PaperComponent.PaperTemplateRequestType.Job:
                HandleJobRequest(contentEntity, args.Actor, args.Index);
                break;
        }

        // BUI открыт на внешнем листе; SetContent обновляет только внутреннюю страницу.
        if (contentEntity.Owner != entity.Owner)
            RefreshUi(entity);
    }

    /// <summary>
    /// UI показывает содержимое текущей страницы пакета и навигацию.
    /// </summary>
    private PaperComponent.PaperBoundUserInterfaceState GetPaperUiState(Entity<PaperComponent> entity)
    {
        var contentUid = ResolvePaperContentTarget(entity);
        if (!_paperQuery.TryComp(contentUid, out var contentPaper))
            contentPaper = entity.Comp;

        var pageEv = new GetPaperPageInfoEvent(entity);
        RaiseLocalEvent(entity.Owner, ref pageEv);

        var templateFieldsEnabled = PaperInteractiveTagParsing.ContainsInteractiveTags(contentPaper.Content);

        return new PaperComponent.PaperBoundUserInterfaceState(
            contentPaper.Content,
            contentPaper.DefaultColor,
            contentPaper.StampedBy,
            entity.Comp.Mode,
            contentPaper.ImageContent,
            contentPaper.ImageScale,
            templateFieldsEnabled,
            pageEv.PageIndex,
            pageEv.PageCount,
            GetNetEntity(contentUid));
    }
}
