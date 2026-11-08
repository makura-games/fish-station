using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Ipc.Modules;

/// <summary>Action: вкл/выкл накладок встроенного дефиба КПБ.</summary>
public sealed partial class FishIpcDefibActionEvent : InstantActionEvent;

/// <summary>Action: jumpstarter — толчок себя из crit.</summary>
public sealed partial class FishIpcJumpstarterActionEvent : InstantActionEvent;

/// <summary>Общий toggle модулей (ремонт / HUD / суставы / питание модуля).</summary>
public sealed partial class FishIpcModuleToggleActionEvent : InstantActionEvent;

/// <summary>Модуль включили или выключили кнопкой. Хост уже обновлён.</summary>
[ByRefEvent]
public readonly record struct FishIpcModulePowerToggledEvent(EntityUid Host, bool Enabled);

[Serializable, NetSerializable]
public sealed partial class FishIpcDefibDoAfterEvent : SimpleDoAfterEvent;
