using System.Linq;

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
}
