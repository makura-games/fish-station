using Content.Shared._Fish.Paper;
using Content.Shared.Paper;
using Robust.Shared.GameObjects;
using static Content.Shared.Paper.PaperComponent;

namespace Content.Client.Paper.UI;

public sealed partial class PaperBoundUserInterface
{
    private NetEntity _contentEntity;

    private void InitializeStapledDocumentSupport()
    {
        if (_window == null)
            return;

        _window.OnPageTurn += delta => SendMessage(new FishPaperPageTurnMessage(delta));
        _window.OnRemovePage += () => SendMessage(new FishPaperRemoveCurrentPageMessage());
    }

    private void UpdateStapledDocumentState(PaperBoundUserInterfaceState state)
    {
        _contentEntity = state.ContentEntity;
    }

    private void SendPaperInputWithContentTarget(string text)
    {
        NetEntity? target = _contentEntity.IsValid() ? _contentEntity : null;
        SendMessage(new PaperInputTextMessage(text, target));
    }
}
