using Robust.Shared.Localization;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Neuro;

/// <summary>
/// Событие-алерт о потере/изменении нейронов.
/// </summary>
[Serializable, NetSerializable]
public sealed class NeuroAlertEvent : EntityEventArgs
{
    public LocId Message;

    public NeuroAlertEvent(LocId message)
    {
        Message = message;
    }
}