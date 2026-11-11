using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Ipc;

[Serializable, NetSerializable]
public sealed partial class FishDeadStartupDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Секунда на вставку батареи в шасси.</summary>
[Serializable, NetSerializable]
public sealed partial class FishIpcBatteryInsertDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Секунда на вынимание батареи из шасси.</summary>
[Serializable, NetSerializable]
public sealed partial class FishIpcBatteryEjectDoAfterEvent : SimpleDoAfterEvent;
