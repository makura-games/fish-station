using Content.Shared.Actions;

namespace Content.Shared.Holopad;

/// <summary>
/// Выйти из ресивера, не завершая звонок.
/// Выдаётся только тому, кто сейчас смотрит через ресивер
/// </summary>
public sealed partial class RemoteHolopadExitViewActionEvent : InstantActionEvent
{
}
