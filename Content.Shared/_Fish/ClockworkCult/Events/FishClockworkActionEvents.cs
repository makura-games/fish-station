using Content.Shared.Actions;

namespace Content.Shared._Fish.ClockworkCult.Events;

/// <summary>
/// Призыв доспехов Ratvar (Clockwork Armaments).
/// </summary>
public sealed partial class FishClockworkCallArmorActionEvent : InstantActionEvent;

/// <summary>
/// Призыв копья Ratvar.
/// </summary>
public sealed partial class FishClockworkCallSpearActionEvent : InstantActionEvent;

/// <summary>
/// Mass Recall Ковчега через Eminence.
/// </summary>
public sealed partial class FishEminenceMassRecallActionEvent : InstantActionEvent;
