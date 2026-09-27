using Robust.Shared.GameStates;

namespace Content.Shared.Holopad;

/// <summary>
/// Маркер транслирующего голопада. Только он может звонить на <see cref="RemoteHolopadReceiverComponent"/>.
/// При активном звонке глаз звонящего переносится на голограмму приёмника.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RemoteHolopadTransmitterComponent : Component
{
    /// <summary>
    /// Тело игрока, которое сейчас смотрит через трансляцию.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? ViewerBody;

    /// <summary>
    /// Принимающий голопад, на который идёт трансляция.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? ReceiverHolopad;

    /// <summary>
    /// Голограмма на приёмнике, через которую смотрит звонящий.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? RemoteHologram;
}