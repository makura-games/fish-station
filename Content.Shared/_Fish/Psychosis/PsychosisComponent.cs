using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Psychosis;

/// <summary>
/// Уровень психоза, вызываемый химикатами, радиацией и т.п.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PsychosisComponent : Component
{
    /// <summary>
    /// Текущий уровень психоза (0–100).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Current = 0f;

    /// <summary>
    /// Словарь источников: имя источника -> сила.
    /// </summary>
    public Dictionary<string, float> Sources = new();
}