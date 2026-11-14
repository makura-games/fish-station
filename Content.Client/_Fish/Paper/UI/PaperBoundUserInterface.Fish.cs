using Content.Shared._Fish.Paper;

namespace Content.Client.Paper.UI;

public sealed partial class PaperBoundUserInterface
{
    private void InitializeStapledDocumentSupport()
    {
        if (_window == null)
            return;

        _window.OnPageTurn += delta => SendMessage(new FishPaperPageTurnMessage(delta));
        _window.OnRemovePage += () => SendMessage(new FishPaperRemoveCurrentPageMessage());
    }
}
