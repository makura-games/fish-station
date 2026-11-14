using Content.Shared.Paper;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Paper.UI;

public sealed partial class PaperWindow
{
    public event Action<int>? OnPageTurn;
    public event Action? OnRemovePage;

    private BoxContainer? _pageBar;
    private Label? _pageLabel;
    private Button? _prevButton;
    private Button? _nextButton;
    private Button? _removeButton;

    private void UpdatePageNavigation(PaperComponent.PaperBoundUserInterfaceState state)
    {
        EnsurePageBar();

        var multi = state.PageCount > 1;
        _pageBar!.Visible = multi;
        if (!multi)
            return;

        _pageLabel!.Text = Loc.GetString("fish-stapled-document-page-indicator",
            ("current", state.PageIndex + 1),
            ("total", state.PageCount));
        _prevButton!.Disabled = state.PageIndex <= 0 || state.Mode == PaperComponent.PaperAction.Write;
        _nextButton!.Disabled = state.PageIndex >= state.PageCount - 1 || state.Mode == PaperComponent.PaperAction.Write;
        _removeButton!.Disabled = state.Mode == PaperComponent.PaperAction.Write;
    }

    private void EnsurePageBar()
    {
        if (_pageBar != null)
            return;

        _pageBar = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(6, 0, 6, 6),
            Visible = false,
        };

        _prevButton = new Button { Text = Loc.GetString("fish-stapled-document-page-prev") };
        _nextButton = new Button { Text = Loc.GetString("fish-stapled-document-page-next") };
        _removeButton = new Button { Text = Loc.GetString("fish-stapled-document-page-remove") };
        _pageLabel = new Label
        {
            HorizontalExpand = true,
            Align = Label.AlignMode.Center,
        };

        _prevButton.OnPressed += _ => OnPageTurn?.Invoke(-1);
        _nextButton.OnPressed += _ => OnPageTurn?.Invoke(1);
        _removeButton.OnPressed += _ => OnRemovePage?.Invoke();

        _pageBar.AddChild(_prevButton);
        _pageBar.AddChild(_pageLabel);
        _pageBar.AddChild(_nextButton);
        _pageBar.AddChild(_removeButton);

        // Вставляем над кнопками редактирования
        ContentsRoot.AddChild(_pageBar);
        var editIndex = ContentsRoot.ChildCount - 1;
        for (var i = 0; i < ContentsRoot.ChildCount; i++)
        {
            if (ContentsRoot.GetChild(i) != EditButtons)
                continue;
            editIndex = i;
            break;
        }

        _pageBar.SetPositionInParent(editIndex);
    }
}
