using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Stress;

/// <summary>
/// Событие, отправляемое клиенту при переходе стресса через порог (например, 75%).
/// </summary>
[Serializable, NetSerializable]
public sealed class StressEffectsEvent : EntityEventArgs
{
    public NetEntity Uid;
    public bool Active; // true если стресс > порога

    public StressEffectsEvent(NetEntity uid, bool active)
    {
        Uid = uid;
        Active = active;
    }
}