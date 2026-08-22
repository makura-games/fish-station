using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.BattleShuttles;

public sealed partial class ToggleBattleShuttleLockEvent : InstantActionEvent
{
}

[Serializable, NetSerializable]
public sealed partial class BattleShuttleLockBusterDoAfterEvent : SimpleDoAfterEvent
{
}
