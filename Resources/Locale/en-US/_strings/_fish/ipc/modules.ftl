ent-FishBaseIpcModule = IPC chassis module
    .desc = An upgrade board that slots into an open IPC chassis.
    .suffix = Fish

ent-FishIpcModuleWireInterface = IPC wire interface module
    .desc = Examining a panel tells you if it is open or closed. Light battery drain. Toggleable.
    .suffix = Fish

ent-FishIpcModuleApcAdapter = IPC APC adapter module
    .desc = Charges faster from APCs. Light battery drain. Toggleable.
    .suffix = Fish

ent-FishIpcModuleVocalMod = IPC vocal modulator module
    .desc = Built-in radio keeps working even when the battery is dead. Toggleable.
    .suffix = Fish

ent-FishIpcModuleToolHand = IPC multitool hand module
    .desc = While on, puts a multitool in a free hand. Toggleable.
    .suffix = Fish

ent-FishIpcModuleMedHud = IPC medical optics module
    .desc = Health bars and icons over your vision. Toggleable.
    .suffix = Fish

ent-FishIpcModuleSecHud = IPC security optics module
    .desc = Job, mindshield and criminal status over your vision. Toggleable.
    .suffix = Fish

ent-FishIpcModuleMeson = IPC meson optics module
    .desc = While on, protects your eyes from welding. Does not show walls or block flashes. Toggleable.
    .suffix = Fish

ent-FishIpcModuleNightVision = IPC thermal vision module
    .desc = Heat overlay of the surroundings. Drains more than regular optics. Toggleable.
    .suffix = Fish

ent-FishIpcModuleNanoRepair = IPC physical repair module
    .desc = Slowly heals blunt, slash and piercing. Not with thermal repair. Toggleable.
    .suffix = Fish

ent-FishIpcModuleReactiveRepair = IPC thermal repair module
    .desc = Slowly heals heat and shock. Not with physical repair. Toggleable.
    .suffix = Fish

ent-FishIpcModuleEmpScreen = IPC EMP screen module
    .desc = Greatly cuts EMP damage and stops a full chassis blackout. Toggleable.
    .suffix = Fish

ent-FishIpcModuleJumpstarter = IPC jumpstarter module
    .desc = In crit, fires a costly get-up pulse with a cooldown. Does not clear lethal damage. Toggleable.
    .suffix = Fish

ent-FishIpcModuleDefib = IPC defibrillator module
    .desc = Pads in both hands to zap others. Cannot zap yourself. Big battery cost. Toggleable.
    .suffix = Fish

ent-FishIpcModuleRadFilter = IPC radiation filter module
    .desc = Cuts radiation and cellular damage. Without it the chassis takes both in full. Toggleable.
    .suffix = Fish

ent-FishActionIpcDefib = Chassis defibrillator pads
    .desc = Deploy integrated defibrillator pads into both hands. Huge battery cost per zap.

ent-FishActionIpcJumpstarter = Jumpstarter pulse
    .desc = Dump capacitor charge to claw out of critical condition.

ent-FishActionIpcToggleRepair = Toggle chassis repair
    .desc = Enable or disable the installed repair module.

ent-FishActionIpcToggleVision = Toggle chassis optics
    .desc = Enable or disable the installed vision module.

ent-FishActionIpcToggleThermal = Toggle chassis thermals
    .desc = Enable or disable thermal vision.

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
fish-ipc-module-duplicate = That module is already installed.
fish-ipc-module-eject = Eject { $module }
fish-ipc-module-need-charge = Not enough battery charge.
fish-ipc-module-cooldown = The module capacitors are still recharging.
fish-ipc-module-defib-self = You cannot defibrillate yourself with chassis pads.
fish-ipc-module-defib-success = The zap stabilizes { $target }.
fish-ipc-module-defib-fail = The zap fails to revive { $target }.
fish-ipc-module-jumpstarter-not-crit = Jumpstarter only works in critical condition.
fish-ipc-module-jumpstarter-success = Jumpstarter pulse gets the chassis moving.
fish-ipc-module-jumpstarter-fail = Not enough — damage is still lethal.
fish-ipc-module-jumpstarter-still-crit = Not enough — the chassis is still critical.
fish-ipc-module-tool-no-hand = No free hand for the integrated tool.
fish-ipc-module-defib-hands = The pads need both hands free.
fish-ipc-module-wire-interface-self = Wire interface active: nearby panels give extra diagnostics.
fish-ipc-module-wire-interface-panel-open = Wire interface: panel open.
fish-ipc-module-wire-interface-panel-closed = Wire interface: panel closed.
fish-ipc-emp-hit-screened = EMP hits the chassis, but the screen bleeds off most of it.
fish-ipc-health-analyzer-refuse = A normal health analyzer cannot read an IPC chassis.

research-technology-fish-ipc-healthcare = IPC Healthcare Modules
research-technology-fish-ipc-utility = IPC Utility Modules
research-technology-fish-ipc-advanced-optics = IPC Advanced Optics
