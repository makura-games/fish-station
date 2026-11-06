ent-FishBaseIpcModule = IPC chassis module
    .desc = An upgrade module for an integrated positronic chassis.
    .suffix = Fish

ent-FishIpcModuleWireInterface = IPC wire interface module
    .desc = A diagnostic interface that highlights chassis wiring. Barely draws power, but occupies a module slot.
    .suffix = Fish

ent-FishIpcModuleApcAdapter = IPC APC adapter module
    .desc = Accelerates chassis charging from APCs and compatible sources. Still finite.
    .suffix = Fish

ent-FishIpcModuleVocalMod = IPC vocal modulator module
    .desc = Keeps built-in radio online even when the chassis is power-starved.
    .suffix = Fish

ent-FishIpcModuleToolHand = IPC tool-hand module
    .desc = Mounts a single integrated crowbar into a free hand. Not a full toolset.
    .suffix = Fish

ent-FishIpcModuleMedHud = IPC medical optics module
    .desc = Built-in medical HUD. Occupies the vision module slot.
    .suffix = Fish

ent-FishIpcModuleSecHud = IPC security optics module
    .desc = Built-in security HUD. Occupies the vision module slot.
    .suffix = Fish

ent-FishIpcModuleMeson = IPC meson optics module
    .desc = Built-in welding eye protection. Occupies the vision module slot.
    .suffix = Fish

ent-FishIpcModuleNightVision = IPC night vision module
    .desc = Low-light optics for the chassis. Occupies the vision module slot.
    .suffix = Fish

ent-FishIpcModuleNanoRepair = IPC nanorepair module
    .desc = Microscopic repair drones slowly mend chassis damage while hungrily draining the battery.
    .suffix = Fish

ent-FishIpcModuleReactiveRepair = IPC reactive repair module
    .desc = Faster micro-repairs than nanorepair, still hungry. Mutually exclusive with nanorepair.
    .suffix = Fish

ent-FishIpcModuleEmpScreen = IPC EMP screen module
    .desc = Partial EMP shielding for the chassis. Not full immunity.
    .suffix = Fish

ent-FishIpcModuleMagneticJoints = IPC magnetic joints module
    .desc = Magnetic couplings resist limb amputation. EMP temporarily disables the effect.
    .suffix = Fish

ent-FishIpcModuleSealedJoints = IPC sealed joints module
    .desc = Sealed couplings block amputation. EMP locks the chassis hard. Mutually exclusive with magnetic joints.
    .suffix = Fish

ent-FishIpcModuleJumpstarter = IPC jumpstarter module
    .desc = Capacitor dump that can claw a chassis out of critical — not death. Long cooldown.
    .suffix = Fish

ent-FishIpcModuleDefib = IPC defibrillator module
    .desc = Integrated defibrillator pads. Consumes a huge slice of the battery per zap.
    .suffix = Fish

ent-FishIpcModuleRadFilter = IPC radiation filter module
    .desc = Cuts radiation and related cellular damage at the cost of constant drain.
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
