using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Ipc.Modules;

/// <summary>Action: встроенный дефиб КПБ (цель — другой моб).</summary>
public sealed partial class FishIpcDefibActionEvent : EntityTargetActionEvent;

/// <summary>Action: jumpstarter — толчок себя из crit.</summary>
public sealed partial class FishIpcJumpstarterActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class FishIpcDefibDoAfterEvent : SimpleDoAfterEvent;
