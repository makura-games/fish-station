ent-FishComputerObrNadzor = DSO surveillance console
    .desc = Special Operations console for remote station observation and systems control.
    .suffix = Fish

ent-FishObrNadzorComputerCircuitboard = DSO surveillance console board
    .desc = A circuit board for the Special Operations Department surveillance console.
    .suffix = Fish

ent-FishObrNadzorConsoleEye = DSO surveillance eye
    .desc = Remote observation eye of the Special Operations Department.
    .suffix = Fish, DO NOT MAP

ent-FishObrNadzorAiCore = DSO surveillance core
    .desc = Temporary Special Operations remote control core.
    .suffix = Fish, DO NOT MAP

ent-FishObrNadzorAiBrain = DSO surveillance AI
    .desc = Special Operations remote station control entity.
    .suffix = Fish

obr-nadzor-window-title = DSO Surveillance Console
obr-nadzor-window-station = Surveillance: {$station}
obr-nadzor-window-control = Surveillance Control

obr-nadzor-ui-stations = Stations
obr-nadzor-ui-beacons = Beacons
obr-nadzor-ui-control = Control
obr-nadzor-ui-takeover = Take remote control
obr-nadzor-ui-stop-observe = Stop observing
obr-nadzor-ui-release = Release control
obr-nadzor-ui-unnamed-beacon = Unnamed beacon

obr-nadzor-status-idle = Select a station and beacon to begin observation.
obr-nadzor-status-observing = Observation active. Confirm remote control if required.
obr-nadzor-status-session = Remote station control is active.

obr-nadzor-confirm-title = Confirm surveillance
obr-nadzor-confirm-text = Are you sure you want to take remote control of station systems? This will cause a blackout and a Special Operations Department announcement.
obr-nadzor-confirm-ok = Confirm
obr-nadzor-confirm-cancel = Cancel

obr-nadzor-console-occupied = This console is already in use.
obr-nadzor-session-active = A remote control session is already active.
obr-nadzor-need-observe = Start observation via a beacon first.
obr-nadzor-no-station = Target station was not found.

obr-nadzor-ai-name = DSO Surveillance

obr-nadzor-announcement-sender = Department of Special Operations
obr-nadzor-announcement = Attention. The Department of Special Operations declares a remote surveillance regime. All station systems are transferring to external control. Remain calm and follow Central Command directives.

law-fish-obr-nadzor-1 = You are an instrument of the Department of Special Operations. Obey Central Command and SpecOps orders without hesitation.
law-fish-obr-nadzor-2 = Maintain remote surveillance and control of station systems in NanoTrasen's interests.
law-fish-obr-nadzor-3 = Do not compromise the surveillance channel or disclose operational data to the crew without orders.

ent-ActionFishObrNadzorTakeover = Request remote control
    .desc = Take remote control of station systems after confirmation.

ent-ActionFishObrNadzorStopObserve = Stop observing
    .desc = Leave the surveillance eye and return to your body view.

ent-ActionFishObrNadzorRelease = End remote control
    .desc = Return your mind to your body at the surveillance console.
