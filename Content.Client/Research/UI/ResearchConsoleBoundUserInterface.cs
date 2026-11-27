using Content.Client._Fish.Research.UI; // Fish-edit: окно консоли исследований
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.Research.UI;

[UsedImplicitly]
public sealed class ResearchConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private FishResearchWindow? _consoleMenu; // Fish-edit: тип окна консоли РНД

    public ResearchConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _consoleMenu = this.CreateWindow<FishResearchWindow>(); // Fish-edit: открываем окно исследований Fish

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

        // Fish-start: окно использует рецепты, дисциплины и названия предметов
        if (!args.WasModified<TechnologyPrototype>() && !args.WasModified<LatheRecipePrototype>() &&
            !args.WasModified<TechDisciplinePrototype>() && !args.WasModified<EntityPrototype>())
            return;
        // Fish-end

        if (State is not ResearchConsoleBoundInterfaceState rState)
            return;

        _consoleMenu?.Refresh(Owner, rState, force: true); // Fish-edit: перестраиваем каталог после перезагрузки
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not ResearchConsoleBoundInterfaceState castState)
            return;
        _consoleMenu?.Refresh(Owner, castState); // Fish-edit: обновляем окно по состоянию консоли
    }
}
