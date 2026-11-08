ent-FishBaseIpcModule = IPC chassis module
    .desc = An upgrade module for an integrated positronic chassis.
    .suffix = Fish

ent-FishIpcModuleWireInterface = IPC wire interface module
    .desc = Draws 0.05 J/s. Examining a panel reports whether it is open or closed. Toggleable.
    .suffix = Fish

ent-FishIpcModuleApcAdapter = IPC APC adapter module
    .desc = Draws 0.1 J/s. APC charge do-after time is multiplied by 0.55. The sip per cycle stays the same. Toggleable.
    .suffix = Fish

ent-FishIpcModuleVocalMod = IPC vocal modulator module
    .desc = Draws 0.15 J/s. Built-in radio keeps working after the battery dies. Toggleable.
    .suffix = Fish

ent-FishIpcModuleToolHand = IPC tool-hand module
    .desc = Draws 0.2 J/s. While on, places a crowbar in a free hand. Toggleable.
    .suffix = Fish

ent-FishIpcModuleMedHud = IPC medical optics module
    .desc = Draws 0.35 J/s. Vision slot: health bars and health icons. Toggleable.
    .suffix = Fish

ent-FishIpcModuleSecHud = IPC security optics module
    .desc = Draws 0.35 J/s. Vision slot: job, mindshield and criminal status icons. Toggleable.
    .suffix = Fish

ent-FishIpcModuleMeson = IPC meson optics module
    .desc = Draws 0.25 J/s. Vision slot. While on, protects the eyes from welding. While off, it does not. Does not show walls and does not block flashes.
    .suffix = Fish

ent-FishIpcModuleNightVision = IPC thermal vision module
    .desc = Draws 0.55 J/s. Vision slot: thermal overlay. Toggleable.
    .suffix = Fish

ent-FishIpcModuleNanoRepair = IPC physical repair module
    .desc = Draws 1.2 J/s above 10% charge. Heals only blunt, slash and piercing (-0.15/s). Not with thermal repair.
    .suffix = Fish

ent-FishIpcModuleReactiveRepair = IPC thermal repair module
    .desc = Draws 1.6 J/s above 12% charge. Heals only heat and shock (-0.4/s). Not with physical repair.
    .suffix = Fish

ent-FishIpcModuleEmpScreen = IPC EMP screen module
    .desc = Draws 0.45 J/s. EMP damage is multiplied by 0.4 and the chassis is not fully disabled. Toggleable.
    .suffix = Fish

ent-FishIpcModuleJumpstarter = IPC jumpstarter module
    .desc = Draws 0.2 J/s. Crit-only pulse: 30% charge, 180 s cooldown. Does not clear lethal damage. Toggleable.
    .suffix = Fish

ent-FishIpcModuleDefib = IPC defibrillator module
    .desc = Draws 0.25 J/s. Pads in both hands. Zap: 40% charge, 4 s, 90 s cooldown, -40 asphyxiation. Cannot zap yourself. Toggleable.
    .suffix = Fish

ent-FishIpcModuleRadFilter = IPC radiation filter module
    .desc = Draws 0.6 J/s. Radiation and cellular damage are multiplied by 0.35. Without it the chassis takes both in full. Toggleable.
    .suffix = Fish

ent-FishActionIpcDefib = Chassis defibrillator pads
    .desc = Deploy integrated defibrillator pads into both hands. Huge battery cost per zap.

ent-FishActionIpcJumpstarter = Jumpstarter pulse
    .desc = Dump capacitor charge to claw out of critical condition.

ent-FishActionIpcToggleRepair = Toggle chassis repair
    .desc = Enable or disable the installed repair module.

ent-FishActionIpcToggleVision = Toggle chassis optics
    .desc = Enable or disable the installed vision module.

ent-FishActionIpcToggleModule = Toggle chassis module
    .desc = Enable or disable this module. A status popup appears.

ent-FishIpcDefibPads = chassis defibrillator pads
    .desc = Integrated shock paddles projected from an IPC chassis module.
    .suffix = Fish

fish-ipc-module-toggle-name = Toggle { $module }
fish-ipc-module-toggle-desc = Enable or disable the module. Disabled modules do nothing and drain no power.
fish-ipc-module-toggled-off = Module disabled.
fish-ipc-module-panel-closed = Open the chassis panel first.
fish-ipc-module-locked = The chassis is locked.
fish-ipc-module-too-many = The chassis already has the maximum number of modules ({ $max }).
fish-ipc-module-whitelist-deny = That module does not fit an IPC chassis.
fish-ipc-module-exclusion = A module of that group is already installed.
fish-ipc-module-eject = Eject { $module }
fish-ipc-module-need-charge = Not enough battery charge.
fish-ipc-module-cooldown = The module capacitors are still recharging.
fish-ipc-module-defib-self = You cannot defibrillate yourself with chassis pads.
fish-ipc-module-defib-success = The zap stabilizes { $target }.
fish-ipc-module-defib-fail = The zap fails to revive { $target }.
fish-ipc-module-jumpstarter-not-crit = Jumpstarter only works in critical condition.
fish-ipc-module-jumpstarter-success = Jumpstarter pulse gets the chassis moving.
fish-ipc-module-jumpstarter-fail = Not enough — damage is still lethal.
fish-ipc-module-tool-no-hand = No free hand for the integrated tool.
fish-ipc-module-wire-interface-self = Wire interface active: nearby panels give extra diagnostics.
fish-ipc-module-wire-interface-panel-open = Wire interface: panel open.
fish-ipc-module-wire-interface-panel-closed = Wire interface: panel closed.
fish-ipc-emp-hit-screened = EMP hits the chassis, but the screen bleeds off most of it.
fish-ipc-health-analyzer-refuse = A normal health analyzer cannot read an IPC chassis.

research-technology-fish-ipc-healthcare = IPC Healthcare Modules
research-technology-fish-ipc-utility = IPC Utility Modules
research-technology-fish-ipc-advanced-optics = IPC Advanced Optics
