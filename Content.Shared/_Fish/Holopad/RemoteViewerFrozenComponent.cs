using Robust.Shared.GameStates;

namespace Content.Shared.Holopad;

/// <summary>
/// Заморозка тела во время просмотра на ресивере
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RemoteViewerFrozenComponent : Component
{
}
