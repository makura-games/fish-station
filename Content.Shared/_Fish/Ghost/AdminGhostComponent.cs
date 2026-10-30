using System;
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

    /// <summary>
    /// Сущность выданного действия <see cref="InteractionToggleAction"/>.
    /// Создаётся при инициализации агоста и удаляется вместе с компонентом.
    /// </summary>
    [AutoNetworkedField]
    public EntityUid? InteractionToggleActionEntity;

    /// <summary>
    /// Запрещено ли призраку взаимодействовать с миром.
    /// Нужно, чтобы случайно не подбирать и не использовать предметы во время ивента.
    /// </summary>
    /// <remarks>
    /// По умолчанию включено: самозапрет действует сразу при входе в агоста.
    /// </remarks>
    [DataField, AutoNetworkedField]
    public bool InteractionRestricted = true;

    /// <summary>
    /// Кулдаун кнопки переключения после нажатия.
    /// </summary>
    /// <remarks>
    /// Сколько секунд нельзя нажимать кнопку повторно, чтобы нельзя было спамить переключениями.
    /// Само переключение при этом происходит сразу.
    /// </remarks>
    [DataField]
    public TimeSpan ToggleCooldown = TimeSpan.FromSeconds(3);
}

/// <summary>
/// Событие переключения самозапрета на взаимодействие с миром.
/// </summary>
public sealed partial class ToggleAghostInteractionActionEvent : InstantActionEvent;
