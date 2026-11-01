using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Xenobiology;

[Serializable, NetSerializable]
public sealed partial class FishXenobiologyCaptureDoAfterEvent : SimpleDoAfterEvent;
