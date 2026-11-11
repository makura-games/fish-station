using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Psychosis;

/// <summary>
/// Отправляется клиенту для применения шейдеров психоза.
/// </summary>
[Serializable, NetSerializable]
public sealed class PsychosisUpdateEvent : EntityEventArgs
{
    public NetEntity Uid;
    public float Level;
    public bool IsInitialState;

    public PsychosisUpdateEvent(NetEntity uid, float level, bool isInitialState = false)
    {
        Uid = uid;
        Level = level;
        IsInitialState = isInitialState;
    }
}