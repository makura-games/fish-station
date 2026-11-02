using Robust.Client.UserInterface;

namespace Content.Client.Xenoarchaeology.Ui;

/// <summary>
/// BUI for hand-held xeno artifact scanner,  server-provided UI updates.
/// </summary>
public sealed class NodeScannerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private AnalysisConsoleMenu? _scannerDisplay; // Fish-edit: сканер использует штатный граф и сведения об узлах.

    /// <inheritdoc />
    protected override void Open()
    {
        base.Open();

        // Fish-start: режим просмотра без извлечения очков и выбора сервера.
        _scannerDisplay = this.CreateWindow<AnalysisConsoleMenu>();
        _scannerDisplay.SetScannerOwner(Owner);
        // Fish-end
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _scannerDisplay?.Dispose();
    }
}
