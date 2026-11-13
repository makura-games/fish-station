using Content.Shared.Actions;

namespace Content.Shared._Fish.ObrNadzor;

public sealed partial class ObrNadzorTakeoverEvent : InstantActionEvent;

public sealed partial class ObrNadzorStopObserveEvent : InstantActionEvent;

public sealed partial class ObrNadzorReleaseSessionEvent : InstantActionEvent;
