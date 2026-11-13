using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Neuro;

/// <summary>
/// Маркер критического состояния (нейроны < 15). На клиенте может использоваться для шейдеров.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NeuroCriticalComponent : Component
{
}