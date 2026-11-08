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

ent-FishIpcModuleNightVision = IPC night vision module
    .desc = Draws 0.55 J/s. Vision slot: green phosphor and a lighting lift. Toggleable.
    .suffix = Fish

ent-FishIpcModuleNanoRepair = IPC nanorepair module
    .desc = Draws 1.2 J/s above 10% charge. Blunt, slash and piercing -0.15/s, heat and shock -0.2/s. Not with reactive repair.
    .suffix = Fish

ent-FishIpcModuleReactiveRepair = IPC reactive repair module
    .desc = Draws 1.6 J/s above 12% charge. Blunt, slash and piercing -0.35/s, heat and shock -0.4/s. Not with nanorepair.
    .suffix = Fish

ent-FishIpcModuleEmpScreen = IPC EMP screen module
    .desc = Draws 0.45 J/s. EMP damage is multiplied by 0.4 and the chassis is not fully disabled. Toggleable.
    .suffix = Fish

ent-FishIpcModuleMagneticJoints = IPC magnetic joints module
    .desc = Draws 0.4 J/s. 65% chance to cancel amputation. EMP disables the effect. Not with sealed joints.
    .suffix = Fish

ent-FishIpcModuleSealedJoints = IPC sealed joints module
    .desc = Draws 0.5 J/s. Blocks amputation. EMP stuns for 12 s and disables the effect. Not with magnetic joints.
    .suffix = Fish

ent-FishIpcModuleJumpstarter = IPC jumpstarter module
    .desc = Draws 0.2 J/s. Crit-only pulse: 30% charge, 180 s cooldown, -15 blunt and -10 slash, piercing, heat and shock. Does not clear lethal damage. Toggleable.
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

ent-FishActionIpcToggleMagneticJoints = Toggle magnetic joints
    .desc = Enable or disable magnetic amputation resistance.

ent-FishActionIpcToggleSealedJoints = Toggle sealed joints
    .desc = Enable or disable sealed amputation lock.

ent-FishIpcDefibPads = chassis defibrillator pads
    .desc = Integrated shock paddles from an IPC chassis module.
    .suffix = Fish

fish-ipc-module-toggle-name = Toggle { $module }
fish-ipc-module-toggle-desc = Enable or disable this module. While off it does nothing and draws no power.
fish-ipc-module-toggled-off = Module is off.
fish-ipc-module-panel-closed = Open the chassis panel first.
fish-ipc-module-locked = The chassis is locked.
fish-ipc-module-too-many = This chassis already has the maximum number of modules ({ $max }).
fish-ipc-module-whitelist-deny = That module is not compatible with an IPC chassis.
fish-ipc-module-exclusion = A module from this group is already installed.
fish-ipc-module-eject = Eject { $module }
fish-ipc-module-need-charge = Insufficient battery charge.
fish-ipc-module-cooldown = Module capacitors are still recharging.
fish-ipc-module-defib-self = You can't defibrillate yourself with chassis pads.
fish-ipc-module-defib-success = Capacitor discharge stabilizes { $target }.
fish-ipc-module-defib-fail = Capacitor discharge fails to revive { $target }.
fish-ipc-module-jumpstarter-not-crit = Jumpstarter only works while critically damaged.
fish-ipc-module-jumpstarter-success = Jumpstarter surge forces the chassis online.
fish-ipc-module-jumpstarter-fail = Jumpstarter surge is not enough — damage is still lethal.
fish-ipc-module-tool-no-hand = No free hand for the integrated tool.
fish-ipc-module-wire-interface-self = Wire interface online: nearby panels reveal extra diagnostics.
fish-ipc-module-wire-interface-panel-open = Wire interface: panel is open.
fish-ipc-module-wire-interface-panel-closed = Wire interface: panel is sealed.
fish-ipc-module-joints-sealed-block = Sealed joints refuse to yield.
fish-ipc-module-joints-magnetic-resist = Magnetic couplings snap the limb back into place!
fish-ipc-module-joints-emp-magnetic = EMP knocks magnetic joints offline!
fish-ipc-module-joints-emp-sealed = EMP locks sealed joints — chassis seize!
fish-ipc-emp-hit-screened = EMP hits the chassis, but the screen bleeds off most of it.
fish-ipc-health-analyzer-refuse = A normal health analyzer cannot read an IPC chassis.

research-technology-fish-ipc-wire-interface = IPC Wire Interface
research-technology-fish-ipc-apc-adapter = IPC APC Adapter
research-technology-fish-ipc-vocal-mod = IPC Vocal Modulator
research-technology-fish-ipc-tool-hand = IPC Tool Hand
research-technology-fish-ipc-med-hud = IPC Medical Optics
research-technology-fish-ipc-sec-hud = IPC Security Optics
research-technology-fish-ipc-meson = IPC Meson Optics
research-technology-fish-ipc-night-vision = IPC Night Vision
research-technology-fish-ipc-nano-repair = IPC Nanorepair
research-technology-fish-ipc-reactive-repair = IPC Reactive Repair
research-technology-fish-ipc-emp-screen = IPC EMP Screen
research-technology-fish-ipc-magnetic-joints = IPC Magnetic Joints
research-technology-fish-ipc-sealed-joints = IPC Sealed Joints
research-technology-fish-ipc-jumpstarter = IPC Jumpstarter
research-technology-fish-ipc-defib = IPC Defibrillator
research-technology-fish-ipc-rad-filter = IPC Radiation Filter
