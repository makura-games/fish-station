using Robust.Shared.Serialization;

namespace Content.Shared.Holopad;

/// <summary>
/// Войти в звонок увидеть окружение и заморозиться на время просмотра.
/// </summary>
[Serializable, NetSerializable]
public sealed class RemoteHolopadJoinViewMessage : BoundUserInterfaceMessage
{
}
