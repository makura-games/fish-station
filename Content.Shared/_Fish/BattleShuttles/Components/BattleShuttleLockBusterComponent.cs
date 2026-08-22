using Robust.Shared.GameStates;

namespace Content.Shared._Fish.BattleShuttles.Components;

/// <summary>
/// Инструмент взлома замка шаттла. Включается через ItemToggle.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BattleShuttleLockBusterComponent : Component
{
    /// <summary>
    /// Включён ли lock buster. По умолчанию выключен.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField]
    public float BreakDelay = 8f;
}
