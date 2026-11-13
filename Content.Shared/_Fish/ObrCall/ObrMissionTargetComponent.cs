namespace Content.Shared._Fish.ObrCall;

/// <summary>
/// Маркер сущности ОБР — при появлении mind получает миссию.
/// Для шаттла миссия берётся с <see cref="ObrCalledShuttleComponent"/>, если поле Mission пусто.
/// </summary>
[RegisterComponent]
public sealed partial class ObrMissionTargetComponent : Component
{
    /// <summary>
    /// Явная миссия (крио-развёртывание). Пустая — искать на шаттле.
    /// </summary>
    [ViewVariables]
    public string Mission = string.Empty;
}
