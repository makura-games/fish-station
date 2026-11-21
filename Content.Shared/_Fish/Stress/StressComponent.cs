using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Stress;

/// <summary>
/// Компонент, хранящий текущий уровень стресса, резисты и адаптацию.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StressComponent : Component
{
    /// <summary>
    /// Текущий стресс (0–100).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Current = 0f;

    /// <summary>
    /// Базовый резист к крови/трупам (в процентах, 0 = нет, 100 = полный иммунитет).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ResistBlood = 0f;

    /// <summary>
    /// Базовый резист к врагам (в процентах).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ResistEnemies = 0f;

    /// <summary>
    /// Временная адаптация к крови/трупам (добавляется к ResistBlood).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AdaptationBlood = 0f;

    /// <summary>
    /// Временная адаптация к врагам.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AdaptationEnemies = 0f;

    /// <summary>
    /// Максимальная адаптация за раунд (20%).
    /// </summary>
    [DataField]
    public float AdaptationMax = 20f;

    /// <summary>
    /// Потеря нейронов за каждый процент адаптации.
    /// </summary>
    [DataField]
    public float AdaptationCost = 0.5f;
}