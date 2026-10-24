using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Antag;

/// <summary>
/// Маркер: персонаж защищён от автоматического выбора целью антагонистов
/// (через черту в редакторе персонажа).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AntagTargetingProtectedComponent : Component;
