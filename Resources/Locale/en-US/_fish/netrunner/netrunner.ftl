fish-netrunner-title = Neural terminal
fish-netrunner-camera-list = Available cameras
fish-netrunner-refresh = Connect / refresh
fish-netrunner-disconnect = Disconnect
fish-netrunner-journal = Session journal
fish-netrunner-target-hint = Use script actions to select a visible target. For area blackout, select the center of the area.
fish-netrunner-resources = RAM: { $ram }/{ $max } | Trace: { $trace }% | Cooling: { $cooldown } s
fish-netrunner-connecting = Connecting to the chair...
fish-netrunner-connected = Remote connection active. Your body is exposed.
fish-netrunner-local = Local mode. Sit in a powered chair to access cameras.
fish-netrunner-casting = Executing program: { $seconds } s
fish-netrunner-duplicate = Only one netrunner implant can be active.
fish-netrunner-unavailable = Program unavailable. Check RAM, cooldown, target and line of sight.
fish-netrunner-success = Program executed.
fish-netrunner-cancelled = Program interrupted or rejected.
fish-netrunner-area-unavailable = Area unavailable. Check memory, cooldown, range and camera coverage.
fish-netrunner-area-empty = No compatible powered lights are available in this area.
fish-netrunner-visor-delivered = Visor delivered. Wear it in the eyes slot for acceleration; monitor your body temperature.
fish-netrunner-overloaded = Your neural interface overheats!
fish-netrunner-neural-warning = Your mindshield implant heats up. External interference detected!
fish-netrunner-headset-warning = Your headset loses its radio connection!
fish-netrunner-sensors-warning = Your suit sensors lose their connection!
fish-netrunner-trace-alert = Unusual network activity detected on the station network. Inspect cameras and device logs.
fish-netrunner-trace-alert-location = Critical network anomaly! Signal source located { $position }. Inspect cameras and device logs.
fish-netrunner-audit-header = Network anomaly log:
fish-netrunner-forensic-anomalies = Network anomalies:
fish-netrunner-log-entry = { $time }: { $program } — { $target } ({ $result })
fish-netrunner-log-success = executed
fish-netrunner-log-failure = interrupted
fish-netrunner-program-open-airlock = Open airlock
fish-netrunner-program-disable-camera = Disrupt camera
fish-netrunner-program-disable-light = Area blackout
fish-netrunner-program-disable-sensors = Disrupt suit sensors
fish-netrunner-program-disable-headset = Disrupt headset
fish-netrunner-program-neural-overload = Neural overload
fish-netrunner-program-short-circuit = Short circuit
fish-netrunner-program-motor-interference = Motor interference
fish-netrunner-program-optical-glitch = Optical glitch
fish-netrunner-program-lock-weapon = Lock energy weapon
fish-netrunner-program-grip-shock = Grip shock
fish-netrunner-program-neural-static = Neural static
fish-netrunner-program-thermal-surge = Thermal surge
fish-netrunner-program-battery-drain = Battery drain
fish-netrunner-program-lock-airlock = Hold airlock
fish-netrunner-program-erase-trace = Buffer purge
fish-netrunner-brand = CYBERSUN INDUSTRIES LLC / NEURAL SYSTEMS
fish-netrunner-terminal-heading = NEURAL TERMINAL
fish-netrunner-shop-heading = SCRIPT EXCHANGE
fish-netrunner-debug-status = DEBUG / UNRESTRICTED
fish-netrunner-ram = WORKING MEMORY / { $ram } of { $max } RAM
fish-netrunner-trace = NETWORK TRACE / { $trace }%
fish-netrunner-terminal-cooldown = Global cooldown: { $seconds } s
fish-netrunner-temperature = Body temperature: { $temperature } °C
fish-netrunner-temperature-unavailable = Thermometry unavailable
fish-netrunner-visor-active = VISOR / ACCELERATED
fish-netrunner-visor-inactive = VISOR / OFFLINE
fish-netrunner-wallet-infinite = Links: ∞ | Scripts: { $count }
fish-netrunner-wallet = Links: { $links } | Scripts: { $count }
fish-netrunner-empty-hint = No scripts purchased yet. Open the shop and select your first programs using neural links.
fish-netrunner-remote-hint = Use ordinary movement controls. Working cameras on your station provide vision. Closing this window preserves the connection.
fish-netrunner-shop-open = Script shop
fish-netrunner-shop-title = Cybersun catalog
fish-netrunner-shop-balance = Neural links: { $links }
fish-netrunner-shop-balance-infinite = NEURAL LINKS / ∞
fish-netrunner-shop-search = Search by name and purpose
fish-netrunner-shop-empty = No matching scripts.
fish-netrunner-shop-category-all = All scripts
fish-netrunner-shop-category-infrastructure = Infrastructure
fish-netrunner-shop-category-covert = Covert
fish-netrunner-shop-category-combat = Combat
fish-netrunner-shop-visor = Visor · { $price } links
fish-netrunner-shop-visor-owned = Visor delivered
fish-netrunner-shop-visor-hint = The visor divides cooldowns by 1.5 and shows mindshields. Body heat per activation doubles. One order per implant; delivery goes to a hand or the floor nearby.
fish-netrunner-shop-upgrades = Implant expansions
fish-netrunner-shop-programs = Scripts
fish-netrunner-shop-earning = Starting credit: 12 links. Each successfully hacked new device grants 1 link. The chair compiles 1 link per quiet minute at up to 30% trace, capped at 20 per implant. Purchases remain in the implant.
fish-netrunner-shop-memory = +{ $gain } RAM · { $price } links
fish-netrunner-shop-speed = Acceleration { $gain }% · { $price } links
fish-netrunner-shop-hardware = Memory: { $ram } RAM · Expansions: { $memory }/{ $maxMemory }
    Acceleration: { $speed }/{ $maxSpeed } · Activation time: { $percent }% of original
fish-netrunner-shop-owned = Purchased
fish-netrunner-shop-buy = Buy · { $price } links
fish-netrunner-shop-stats = RAM: { $ram } · Trace: { $trace }% · Activation: { $cast } s · Cooldown: { $cooldown } s
fish-netrunner-shop-unavailable = Purchase unavailable. Check your balance and upgrade limits, and finish any current activation.
fish-netrunner-combat-warning = Your electronics have been subjected to network interference!
fish-netrunner-weapon-jammed = The weapon's electronic controller is temporarily locked.
fish-netrunner-uplink-name = Netrunner kit
    .suffix = Fish
fish-netrunner-uplink-description = An implant with 12 initial neural links, a chair board, a jacket and a Cybersun instruction sheet. Scripts, memory expansions, accelerators and the visor are purchased in a separate shop. The chair provides a mobile camera view, faster memory recovery and link compilation. Interference leaves a trace and heats the body.

ent-FishNetrunnerImplant = netrunner implant
    .desc = A compact neural interface with a miniature cooling assembly.
    .suffix = Fish

ent-FishNetrunnerImplanter = netrunner implanter
    .desc = A disposable syringe containing a neural interface.
    .suffix = Fish

ent-FishNetrunnerImplantDebug = debug netrunner implant
    .desc = A demonstration neural terminal with unrestricted software access.
    .suffix = Fish

ent-FishNetrunnerImplanterDebug = debug netrunner implanter
    .desc = An administrative demonstration syringe containing a neural terminal.
    .suffix = Fish

ent-FishClothingOuterCyberpunkJacket = cyberpunk jacket
    .desc = The jacket of a legendary cyberpsycho.
    .suffix = Fish

ent-FishClothingEyesNetrunnerVisor = netrunner visor
    .desc = A neural accelerator behind tinted lenses. Its frame grows warm against the skin.
    .suffix = Fish

ent-FishNetrunnerKit = netrunner kit
    .desc = A neural interface and a circuit board packed in protective foam.
    .suffix = Fish

ent-FishNetrunnerChairCircuitboard = netrunner chair board
    .desc = A machine circuit board with an unusually large neural interface connector.
    .suffix = Fish

ent-FishNetrunnerChair = netrunner chair
    .desc = A reclining terminal with a neural interface and a cooling unit.
    .suffix = Fish

ent-FishActionNetrunnerTerminal = netrunner terminal
    .desc = Check memory, camera connection and recent programs, or open the script shop.
    .suffix = Fish

ent-FishActionNetrunnerJumpToChair = jump to chair
    .desc = Returns your netrunner eye back to the chair.
    .suffix = Fish

ent-FishActionNetrunnerOpenAirlock = open airlock
    .desc = Opens one powered, unbolted airlock. Physical locks are not bypassed.
    .suffix = Fish

ent-FishActionNetrunnerDisableCamera = disrupt camera
    .desc = Disables one camera for 20 seconds. Its image and neural coverage become unavailable.
    .suffix = Fish

ent-FishActionNetrunnerDisableLight = area blackout
    .desc = Suppresses powered lights in a 7 meter radius for 5 minutes. Select the center of the area; remote operation only affects lights covered by cameras.
    .suffix = Fish

ent-FishActionNetrunnerDisableSensors = disrupt suit sensors
    .desc = Suppresses reports from a target's worn suit sensors for 20 seconds.
    .suffix = Fish

ent-FishActionNetrunnerDisableHeadset = disrupt headset
    .desc = Blocks a target's equipped headset transmission and reception for 15 seconds.
    .suffix = Fish

ent-FishActionNetrunnerNeuralOverload = neural overload
    .desc = Covertly overloads a genuine mindshield implant and paralyzes its wearer for 6 seconds. No target warning or security notification is transmitted.
    .suffix = Fish

ent-FishNetrunnerManual = neural terminal manual
    .desc = An instruction sheet bearing the Cybersun Industries LLC logo.
    .suffix = Fish

ent-FishNetrunnerRemoteEye = netrunner eye
    .desc = A mobile viewer for a neural interface.
    .suffix = Fish

stack-fish-coolant-pack = coolant pack
fish-coolant-pack-already-cool = The body is already cool enough.

ent-FishCoolantPack = coolant pack
    .desc = A medical pack filled with endothermic gel. When applied, it rapidly draws excess heat from an overheated body down to a safe 20 °C.
    .suffix = Fish

ent-FishCoolantPack1 = coolant pack
    .desc = A medical pack filled with endothermic gel. When applied, it rapidly draws excess heat from an overheated body down to a safe 20 °C.
    .suffix = Fish

ent-FishActionNetrunnerShortCircuit = short circuit
    .desc = Damages a borg's or IPC's electronic chassis with an electrical overload and knocks them down for 5 seconds. Organic targets are incompatible.
    .suffix = Fish

ent-FishActionNetrunnerMotorInterference = motor interference
    .desc = Slows a borg, IPC or a genuine mindshield implant wearer by 35 percent for 7 seconds. Does not paralyze the target.
    .suffix = Fish

ent-FishActionNetrunnerOpticalGlitch = optical glitch
    .desc = Obscures a borg's or IPC's optics or a worn electronic HUD visor for 3 seconds. Removing the visor restores sight immediately.
    .suffix = Fish

ent-FishActionNetrunnerLockWeapon = lock energy weapon
    .desc = Prevents an energy gun from firing for 6 seconds. Target the gun or a person holding it in their active hand; conventional firearms are incompatible.
    .suffix = Fish

ent-FishActionNetrunnerGripShock = grip shock
    .desc = Makes a borg, IPC or a genuine mindshield wearer drop their active item. Unimplanted humans are incompatible.
    .suffix = Fish

ent-FishActionNetrunnerNeuralStatic = neural static
    .desc = Inflicts 30 stamina damage on a borg, IPC or a genuine mindshield wearer, subject to resistance. One activation does not guarantee a stun.
    .suffix = Fish

ent-FishActionNetrunnerThermalSurge = thermal surge
    .desc = Raises an IPC's or genuine mindshield wearer's temperature by 12 degrees ignoring heat resistance. Deals 20 stamina damage to borgs without thermal regulation.
    .suffix = Fish

ent-FishActionNetrunnerBatteryDrain = battery drain
    .desc = Removes up to 30 percent of a borg's, IPC's or energy weapon's full battery capacity. Target the gun or its holder with the gun in the active hand; conventional firearms are incompatible.
    .suffix = Fish

ent-FishActionNetrunnerLockAirlock = hold airlock
    .desc = Prevents a closed, powered, unbolted airlock from opening for 12 seconds. Does not change bolts or close an already open passage.
    .suffix = Fish

ent-FishActionNetrunnerEraseTrace = buffer purge
    .desc = Clears successful netrunner intervention traces detectable by a forensic scanner on a visible object. The operator's journal remains intact.
    .suffix = Fish

fish-netrunner-manual-content =
    { "[head=1]" }Manual: Neural Terminal[/head]
    ================================================
    Thank you for purchasing the [bold]Neural Terminal[/bold] kit from [bold]Cybersun Industries LLC[/bold]!

    { "[head=2]" }1. Contents and installation[/head]
    The kit includes an implanter, a neural interface chair board, a coolant pack, this holographic manual, and a corporate jacket (delivered separately from the box). Metal, cable, manipulators and power must be obtained separately. Install the implant and invoke the neural terminal interface. A new implant starts with no purchased scripts and receives [bold]12 neural links[/bold] once. Open [bold]Script shop[/bold] to download programs and expansions into quick access.

    { "[head=2]" }2. Hardware expansions[/head]
    Three memory expansions each add [bold]2 RAM[/bold] (max [bold]18 RAM[/bold]). Three activation accelerators each remove [bold]10%[/bold] of base activation time (max [bold]30%[/bold]). Successive upgrades of the same type cost [bold]6, 10 and 14 links[/bold]. Added memory starts empty and fills during recovery. Neural overload always requires at least 4 seconds to activate.
    The [bold]netrunner visor[/bold] costs [bold]19 links[/bold] (one order per implant). Worn over the eyes, it divides cooldowns by 1.5, doubles body heating per activation and outlines genuine mindshield wearers with a blue-cyan contour.

    { "[head=2]" }3. Local operation[/head]
    Select a purchased script and a visible compatible target (6 m range for infrastructure, 5 m for most combat scripts, 6 m center for area blackout). Walls block local connections.
    Movement does not cancel activation while the target stays in sight; damage, losing the target, or interacting with your surroundings cancels execution. An interrupted script's cooldown is capped at [bold]4 seconds[/bold]. Baseline recovery is 1 RAM every 10 seconds after an 8-second delay.

    { "[head=2]" }4. Chair and netrunner eye[/head]
    Assemble a machine frame with the board, [bold]5 steel, 5 cable and 2 manipulators[/bold], anchor and power it, then sit in the chair. Connection takes 5 seconds and 1 RAM, switches view to the [bold]netrunner eye[/bold] at the nearest camera and enables [bold]Jump to chair[/bold].
    Guide the eye through bulkheads with ordinary movement effort. Working cameras on your station and the area around your chair provide vision; uncovered areas show static. Visible objects can be examined in eye mode. Scripts cancel if the target leaves vision or enters static. Minimizing the terminal interface preserves the connection; use [bold]Disconnect[/bold] or unbuckle to exit.

    { "[head=2]" }5. Cooling and neural links[/head]
    A powered chair restores [bold]0.4 RAM/s[/bold], decays trace by [bold]0.3%/s[/bold], and consumes 1 RAM upkeep every 20 seconds. Each newly hacked device grants [bold]1 neural link[/bold] once. Quiet operation at up to 30% trace compiles 1 link per 60 seconds (up to 20 per implant). Credits, scripts and upgrades survive extraction.

    { "[head=2]" }6. Script compatibility[/head]
    { "[bold]" }Infrastructure:[/bold] Open airlock opens a powered unbolted airlock; Hold airlock prevents opening a closed airlock for 12 s without changing bolts. Both actions are recorded in the airlock's access log (readable with a log probe). Disrupt camera disables a camera for 20 s. Area blackout suppresses powered lights in a 7 m radius for 5 minutes.
    { "[bold]" }Covert:[/bold] Disrupt suit sensors (20 s), Disrupt headset (15 s), and Buffer purge (clears successful intervention traces on the target; airlock records also expire over time).
    { "[bold]" }Combat:[/bold] Short circuit (electric damage and 5 s knockdown on borgs or IPCs), Motor interference (35% slow for 7 s on borgs, IPCs or mindshielded), Optical glitch (3 s blindness on borgs, IPCs or HUD glasses), Lock energy weapon (6 s), Grip shock, Neural static (30 stamina damage), Thermal surge (+12 °C ignoring heat resistance, or 20 stamina damage on borgs), Battery drain (up to 30% on borgs, IPCs or energy weapons).
    { "[bold]" }Neural overload[/bold] requires a genuine mindshield: 6 s activation, 6 s paralysis, 10 RAM, 90% trace, 180 s cooldown, with no target or channel alerts.

    { "[head=2]" }7. Operating risks[/head]
    Your body remains vulnerable in the chair. Every activation heats the body by [bold]0.135 °C per RAM[/bold] normally and [bold]0.27 °C per RAM[/bold] with the visor. Interruption does not undo heat already applied. Use a [bold]coolant pack[/bold] to rapidly cool the body down to 20 °C.
    Some scripts allow up to 4 RAM deficit (RAM stops at 0 while the deficit and rapid bursts cause bleeding, brief paralysis and extra trace). Trace thresholds of 30 and 60% may broadcast warnings on the binary channel, while reaching 90% also reveals the operator's or chair's nearest beacon location (except neural overload). Cybersun Industries LLC does not guarantee concealment during combat use.
