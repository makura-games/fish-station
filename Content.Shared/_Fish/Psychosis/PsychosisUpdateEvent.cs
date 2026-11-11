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

    public PsychosisUpdateEvent(NetEntity uid, float level)
    {
        Uid = uid;
        Level = level;
    }
}