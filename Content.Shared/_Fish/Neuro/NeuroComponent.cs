using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Neuro;

/// <summary>
/// Компонент когнитивных ресурсов (нейронов). Скрытая шкала.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NeuroComponent : Component
{
    /// <summary>
    /// Текущее значение нейронов (0–100).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Current = 100f;

    /// <summary>
    /// Максимальное значение (обычно 100).
    /// </summary>
    [DataField]
    public float Max = 100f;

    /// <summary>
    /// Скорость естественного восстановления (ед./сек).
    /// </summary>
    [DataField]
    public float NaturalRegenRate = 1f / 240f; // 1 ед. за 4 минуты

    /// <summary>
    /// Максимальный уровень стресса, при котором возможно восстановление.
    /// </summary>
    [DataField]
    public float RegenStressThreshold = 20f;

    /// <summary>
    /// Безвозвратная потеря нейронов (например, от адаптации).
    /// </summary>
    [DataField]
    public float PermanentLoss = 0f;
}