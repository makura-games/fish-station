using Robust.Shared.Network;
using Robust.Shared.GameStates;
namespace Content.Shared.Holopad;

/// <summary>
/// Маркер принимающего голопада. Пассивный получатель трансляции.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RemoteHolopadReceiverComponent : Component
{
}