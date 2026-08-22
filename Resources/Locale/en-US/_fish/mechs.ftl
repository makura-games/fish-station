mech-internal-damage-applied = Internal systems damaged!
mech-internal-damage-repaired-power = Power bus stabilized.
mech-internal-damage-repaired-hull = Hull / thermal loop restored.
mech-internal-damage-repaired-drive = Drive unjammed.

mech-facing-armor-deflect = Attack glanced off the armor!

mech-overload-on = Drive boost engaged.
mech-overload-off = Drive boost disengaged.
mech-overload-too-damaged = Chassis too damaged for drive boost.

mech-defence-on = Defensive stance engaged.
mech-defence-off = Defensive stance disengaged.

mech-thrusters-on = Maneuvering thrusters online.
mech-thrusters-off = Maneuvering thrusters offline.

mech-smoke-launched = Smoke deployed! Charges left: {$charges}.
mech-smoke-empty = No smoke charges left.
mech-smoke-cooldown = Smoke system recharging.
mech-smoke-failed = Cannot deploy smoke here.

mech-strafe-on = Lateral slide enabled.
mech-strafe-off = Lateral slide disabled.

mech-equipment-swap-popup = Primary equipment: {$item}
mech-equipment-swap-none-popup = Primary equipment: fists

mech-dna-lock-set = Biometric lock set.
mech-dna-lock-cleared = Biometric lock cleared.
mech-dna-lock-denied = Biometric mismatch — entry denied.
mech-dna-lock-no-dna = You have no biometrics to lock with.

mech-maint-ready = Service mode off — chassis ready.
mech-maint-service-hold = Service hold: movement locked.
mech-maint-access-panel = Service access panel open.
mech-maint-blocks-equipment = Cannot install equipment outside ready state.

mech-ui-status-ok = Systems nominal.
mech-ui-internal-damage = Internal faults: {$flags}
mech-ui-overload-active = Drive boost ACTIVE
mech-ui-defence-active = Defence ACTIVE
mech-ui-thrusters-active = Thrusters ACTIVE
mech-ui-strafe-active = Slide ACTIVE
mech-ui-dna-locked = Biometric lock engaged
mech-ui-maintenance = Service: {$state}

ent-MechOdysseus = Odysseus
    .desc = A medical exosuit designed for patient recovery and emergency response.

ent-MechOdysseusBattery = Odysseus
    .suffix = Battery
    .desc = A medical exosuit designed for patient recovery and emergency response.

ent-MechOdysseusFilled = Odysseus
    .suffix = Filled
    .desc = A medical exosuit designed for patient recovery and emergency response.

ent-MechEquipmentSleeper = cabin medbay module
    .desc = Holds one patient and doses reagents from chassis chemical reserves.

ent-MechEquipmentRescueJaw = rescue jaws
    .desc = Hydraulic jaws for forcing doors and clearing obstacles during rescue.

ent-MechEquipmentSyringeGun = mech syringe gun
    .desc = A mounted pneumatic syringe launcher for rapid reagent delivery.

mech-internals-on = Cabin air reserve engaged.
mech-internals-off = Cabin air reserve disengaged.
mech-radio-mic-on = Radio microphone on.
mech-radio-mic-off = Radio microphone off.
mech-radio-speaker-on = Radio speaker on.
mech-radio-speaker-off = Radio speaker off.
mech-zoom-on = Optical zoom engaged — movement locked.
mech-zoom-off = Optical zoom disengaged.
mech-phasing-on = Phase mode online.
mech-phasing-off = Phase mode offline.
mech-damtype-cycled = Melee damage type: {$type}
mech-wreckage-empty = Nothing left to salvage.
mech-wreckage-salvaged = Salvaged equipment from the wreckage.
mech-wreckage-scrap = Salvaged scrap metal.
mech-ui-internals-on = Cabin air: ON
mech-ui-internals-off = Cabin air: OFF
mech-ui-zoom-active = Zoom ACTIVE
mech-ui-phasing-active = Phase ACTIVE
mech-tracking-title = Mech Tracking
mech-tracking-refresh = Refresh
mech-tracking-no-pilot = (empty)
mech-tracking-broken = BROKEN
mech-tracking-ok = OK
mech-tracking-entry = {$name} | hull {$integrity}% | power {$energy}% | pilot {$pilot} | {$status}

mech-sleeper-patient = Patient: {$name}
mech-sleeper-patient-empty = Patient: none
mech-sleeper-patient-unknown = unknown
mech-sleeper-eject = Eject patient
mech-sleeper-reagents-header = Injectable reagents
mech-sleeper-inject-hint = Click a reagent to inject {$amount} u
mech-sleeper-reagent-entry = {$name} ({$quantity})
mech-sleeper-no-patient = No patient in the sleeper.
mech-sleeper-no-reagents = No reagents available.
mech-sleeper-inject-failed = Failed to inject reagent.
mech-sleeper-injected = Injected {$amount} u of {$reagent} into {$patient}.

ent-MechWreckage = mech wreckage
    .desc = The twisted remains of an exosuit. Crowbar salvage may yield parts.

ent-MechBayPad = mech bay charger
    .desc = A floor pad that recharges parked exosuit power cages.

ent-ComputerMechTracking = mech tracking console
    .desc = Tracks registered exosuit beacons across the station.

ent-MechTrackingComputerCircuitboard = mech tracking computer board
    .desc = A computer printed circuit board for a mech tracking console.
