using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Ipc;

[Serializable, NetSerializable]
public sealed partial class FishBatteryDrinkerDoAfterEvent : SimpleDoAfterEvent;
