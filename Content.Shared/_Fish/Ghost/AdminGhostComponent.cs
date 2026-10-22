using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Ghost;

/// <summary>
/// Помечает админского призрака (aghost) и хранит его персональные настройки.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(AdminGhostSystem))]
public sealed partial class AdminGhostComponent : Component
{
    /// <summary>
    /// Действие, переключающее самозапрет на взаимодействие с миром.
    /// </summary>
    [DataField]
    public EntProtoId InteractionToggleAction = "FishActionAghostToggleInteraction";

    [DataField, AutoNetworkedField]
    public EntityUid? InteractionToggleActionEntity;

    /// <summary>
    /// Запрещено ли призраку взаимодействовать с миром.
    /// Нужно, чтобы случайно не подбирать и не использовать предметы во время ивента.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool InteractionRestricted;
}

/// <summary>
/// Событие переключения самозапрета на взаимодействие с миром.
/// </summary>
public sealed partial class ToggleAghostInteractionActionEvent : InstantActionEvent;
