using Content.Shared.Research.Components;
// Fish-edit: прототипы обрабатывает окно дерева.
using Content.Client._Fish.Research.UI; // Fish-edit: окно дерева исследований.
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.Research.UI;

[UsedImplicitly]
public sealed class ResearchConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private FishResearchConsoleMenu? _consoleMenu; // Fish-edit: окно дерева исследований.

    public ResearchConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        var owner = Owner;

        _consoleMenu = this.CreateWindow<FishResearchConsoleMenu>(); // Fish-edit: окно дерева исследований.
        _consoleMenu.SetEntity(owner);

        _consoleMenu.OnTechnologyCardPressed += id =>
        {
            SendMessage(new ConsoleUnlockTechnologyMessage(id));
        };

        _consoleMenu.OnServerButtonPressed += () =>
        {
            SendMessage(new ConsoleServerSelectionMessage());
        };
    }

    public override void OnProtoReload(PrototypesReloadedEventArgs args)
    {
        base.OnProtoReload(args);

        // Fish-start: окно само обновляет кеш затронутых технологий и рецептов.
        _consoleMenu?.OnProtoReload(args);
        // Fish-end
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not ResearchConsoleBoundInterfaceState castState)
            return;
        _consoleMenu?.UpdatePanels(castState);
        _consoleMenu?.UpdateInformationPanel(castState);
    }
}
