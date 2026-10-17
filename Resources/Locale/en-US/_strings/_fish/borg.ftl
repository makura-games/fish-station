## Borg Mk2 (borgType: FishGenericMk2, FishEngineeringMk2, FishMiningMk2, FishJanitorMk2, FishMedicalMk2,
## FishServiceMk2, FishPeaceMk2, FishClownMk2, FishSecurityMk2). Скрыты из меню выбора специализации
## (hideInMenu), назначаются только через отдельный Mk2-chassis.
borg-type-fish-generic-mk2-name = Generic Mk2
borg-type-fish-generic-mk2-desc = A hardened variant of the generic cyborg, fitted with upgraded plating and internal systems. Does a bit of everything, only tougher.
borg-type-fish-generic-mk2-transponder = generic Mk2 cyborg

borg-type-fish-engineering-mk2-name = Engineering Mk2
borg-type-fish-engineering-mk2-desc = A hardened variant of the engineering cyborg, fitted with upgraded plating and internal systems. Assists the engineering team with reinforced hull and tools.
borg-type-fish-engineering-mk2-transponder = engineering Mk2 cyborg

borg-type-fish-mining-mk2-name = Salvage Mk2
borg-type-fish-mining-mk2-desc = A hardened variant of the salvage cyborg, fitted with upgraded plating and internal systems. Mines, scavenges and fights wildlife behind reinforced armour.
borg-type-fish-mining-mk2-transponder = salvage Mk2 cyborg

borg-type-fish-janitor-mk2-name = Janitor Mk2
borg-type-fish-janitor-mk2-desc = A hardened variant of the janitor cyborg, fitted with upgraded plating and internal systems. Keeps the station spotless behind reinforced armour.
borg-type-fish-janitor-mk2-transponder = janitor Mk2 cyborg

borg-type-fish-medical-mk2-name = Medical Mk2
borg-type-fish-medical-mk2-desc = A hardened variant of the medical cyborg, fitted with upgraded plating and internal systems. Treats the crew where conventional paramedics cannot reach.
borg-type-fish-medical-mk2-transponder = medical Mk2 cyborg

borg-type-fish-service-mk2-name = Service Mk2
borg-type-fish-service-mk2-desc = A hardened variant of the service cyborg, fitted with upgraded plating and internal systems. Serves the crew with a much sturdier frame.
borg-type-fish-service-mk2-transponder = service Mk2 cyborg

borg-type-fish-peace-mk2-name = Peace Mk2
borg-type-fish-peace-mk2-desc = A hardened variant of the peacekeeping cyborg, fitted with upgraded plating and internal systems. Keeps order through de-escalation behind reinforced armour.
borg-type-fish-peace-mk2-transponder = peace Mk2 cyborg

borg-type-fish-clown-mk2-name = Clown Mk2
borg-type-fish-clown-mk2-desc = A hardened variant of the clown cyborg, fitted with upgraded plating and internal systems. HONK! Same comedy, sturdier cybernetics.
borg-type-fish-clown-mk2-transponder = clown Mk2 cyborg

borg-type-fish-security-mk2-name = Security Mk2
borg-type-fish-security-mk2-desc = A hardened variant of the security cyborg, fitted with upgraded plating and internal systems.
borg-type-fish-security-mk2-transponder = security Mk2 cyborg

## Borg Upgrade Module (item FishBorgUpgradeModule): interruptible 15-second conversion of a
## standard cyborg into its Mk2 variant. The entity is not recreated - only its borgType changes.
borg-upgrade-invalid-target = This cyborg can't be upgraded.
borg-upgrade-already-mk2 = This cyborg is already a Mk2.
borg-upgrade-in-progress = Another upgrade is already in progress.
borg-upgrade-complete = The cyborg has been upgraded to Mk2.

## Borg rest action (entity FishBorgRestAction): toggles the resting state of Mk2 borgs.
## While resting: movement and all actions except this one are disabled, power drain is reduced.
ent-FishBorgRestAction = Rest
    .desc = Lie down and rest. Movement and actions are disabled while resting and power drain is reduced. Press again to stand up.
