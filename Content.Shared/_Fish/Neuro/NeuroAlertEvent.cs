using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Neuro;

/// <summary>
/// Событие-алерт о потере/изменении нейронов.
/// </summary>
[Serializable, NetSerializable]
public sealed class NeuroAlertEvent : EntityEventArgs
{
    public string Message;

    public NeuroAlertEvent(string message)
    {
        Message = message;
    }
}