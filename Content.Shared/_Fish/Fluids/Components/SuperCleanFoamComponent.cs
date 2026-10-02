using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Fluids.Components;

/// <summary>
/// Компонент пены, которая при распространении/появлении на тайле удаляет мусор и восстанавливает разбитые лампочки.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SuperCleanFoamComponent : Component
{
}
