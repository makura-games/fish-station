using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Holopad;

/// <summary>
/// Маркер транслирующего голопада
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RemoteHolopadTransmitterComponent : Component
{
    /// <summary>
    /// Игроки, которые сейчас смотрят через этот голопад
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> Viewers = new();

    /// <summary>
    /// Выйти из просмотра, выдаётся каждому Viewer на время просмотра.
    /// </summary>
    [DataField]
    public EntProtoId ExitViewAction = "ActionRemoteHolopadExitView";
}

/// <summary>
/// Маркер принимающего голопада
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RemoteHolopadReceiverComponent : Component
{
}
