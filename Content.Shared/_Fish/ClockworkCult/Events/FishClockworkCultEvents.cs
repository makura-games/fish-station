using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.ClockworkCult.Events;

/// <summary>
/// DoAfter чтения писания со слэба.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class FishClockworkScriptureReciteDoAfterEvent : SimpleDoAfterEvent
{
    public string ScripturePrototypeId = string.Empty;
}

/// <summary>
/// Событие: слуга добавлен/удалён.
/// </summary>
public sealed class FishClockworkServantChangedEvent : EntityEventArgs
{
    public EntityUid Servant;
    public bool Added;

    public FishClockworkServantChangedEvent(EntityUid servant, bool added)
    {
        Servant = servant;
        Added = added;
    }
}

/// <summary>
/// Событие победы/поражения культа.
/// </summary>
public sealed class FishClockworkCultEndedEvent : EntityEventArgs
{
    public FishClockworkWinCondition Condition;

    public FishClockworkCultEndedEvent(FishClockworkWinCondition condition)
    {
        Condition = condition;
    }
}
