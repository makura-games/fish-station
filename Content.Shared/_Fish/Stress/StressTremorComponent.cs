using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Stress;

/// <summary>
/// Добавляется при высоком стрессе, вызывает дрожание (импульсы).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StressTremorComponent : Component
{
    /// <summary>
    /// Сила тремора (0–1), зависит от уровня стресса.
    /// </summary>
    [DataField]
    public float Intensity = 0.5f;

    /// <summary>
    /// Интервал между импульсами (в секундах).
    /// </summary>
    [DataField]
    public float ImpulseInterval = 0.5f;
}