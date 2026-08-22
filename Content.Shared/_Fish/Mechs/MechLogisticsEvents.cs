using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Mechs;

/// <summary>
/// Mech сломан — для спавна wreckage (Fish).
/// </summary>
[ByRefEvent]
public readonly record struct MechBrokenEvent(EntityUid Mech);

[Serializable, NetSerializable]
public sealed partial class MechWreckageSalvageDoAfterEvent : SimpleDoAfterEvent;
