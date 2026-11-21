using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Input.Binding;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client._Fish.Skills;

/// <summary>
/// Кнопка топбара и хоткей открытия меню навыков.
/// </summary>
[UsedImplicitly]
public sealed partial class SkillsUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [UISystemDependency] private readonly SkillSystem _skills = default!;

    private MenuButton? SkillsButton => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.SkillsButton;

    public void OnStateEntered(GameplayState state)
    {
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenSkillsMenu,
                InputCmdHandler.FromDelegate(_ => RequestOpen()))
            .Register<SkillsUIController>();
    }

    public void OnStateExited(GameplayState state)
    {
        CommandBinds.Unregister<SkillsUIController>();
    }

    public void UnloadButton()
    {
        if (SkillsButton == null)
            return;

        SkillsButton.OnPressed -= OnPressed;
    }

    public void LoadButton()
    {
        if (SkillsButton == null)
            return;

        SkillsButton.OnPressed += OnPressed;
    }

    private void OnPressed(ButtonEventArgs args)
    {
        RequestOpen();
    }

    private void RequestOpen()
    {
        if (SkillsButton is { } button)
            button.Pressed = false;

        _skills.RequestOpenSkillsUi();
    }
}
