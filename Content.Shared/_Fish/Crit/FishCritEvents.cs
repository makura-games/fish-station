using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Crit;

[Serializable, NetSerializable]
public sealed partial class FishCritCprDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Блокирует стак CPR на одной цели.</summary>
[RegisterComponent]
public sealed partial class FishCritCprActiveComponent : Component;
