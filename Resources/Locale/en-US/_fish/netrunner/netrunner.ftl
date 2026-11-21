fish-netrunner-title = Neural terminal
fish-netrunner-camera-list = Available cameras
fish-netrunner-refresh = Connect / refresh
fish-netrunner-disconnect = Disconnect
fish-netrunner-journal = Session journal
fish-netrunner-target-hint = Use script actions to select a visible target. For area blackout, select the center of the area.
fish-netrunner-resources = RAM: { $ram }/{ $max } | Trace: { $trace }% | Cooling: { $cooldown } s
fish-netrunner-connecting = Connecting to the chair...
fish-netrunner-connected = Remote connection active. Your body is exposed.
fish-netrunner-local = Local mode. Lie in a powered chair to access cameras.
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
fish-netrunner-trace-alert = Unusual network activity detected near sector ({ $x }, { $y }). Inspect camera and device logs.
fish-netrunner-neural-alert = Neural interference detected near sector ({ $x }, { $y }). Check surveillance cameras.
fish-netrunner-audit-header = Network anomaly log:
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
fish-netrunner-remote-hint = Use ordinary movement controls. Working cameras on your station provide vision. Moving the camera does not interrupt script activation. Closing this window preserves the connection.
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
fish-netrunner-uplink-description = An implant with 12 initial neural links, a chair board, a jacket (delivered separately from the box) and a Cybersun instruction sheet. Scripts, memory expansions, accelerators and the visor are purchased in a separate shop. The chair provides a mobile camera view, faster memory recovery and link compilation. Interference leaves a trace and heats the body.

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

ent-FishNetrunnerRemoteEye = neural camera viewer
    .desc = A mobile viewer for a neural interface.
    .suffix = Fish

ent-FishActionNetrunnerShortCircuit = short circuit
    .desc = Damages a borg's electronic chassis with an electrical overload. Organic targets are incompatible.
    .suffix = Fish

ent-FishActionNetrunnerMotorInterference = motor interference
    .desc = Slows a borg or a genuine mindshield implant wearer by 35 percent for 7 seconds. Does not paralyze the target.
    .suffix = Fish

ent-FishActionNetrunnerOpticalGlitch = optical glitch
    .desc = Obscures a borg's vision or a worn electronic HUD visor for 3 seconds. Removing the visor restores sight immediately.
    .suffix = Fish

ent-FishActionNetrunnerLockWeapon = lock energy weapon
    .desc = Prevents an energy gun from firing for 6 seconds. Target the gun or a person holding it in their active hand; conventional firearms are incompatible.
    .suffix = Fish

ent-FishActionNetrunnerGripShock = grip shock
    .desc = Makes a borg or a genuine mindshield wearer drop their active item. Unimplanted humans are incompatible.
    .suffix = Fish

ent-FishActionNetrunnerNeuralStatic = neural static
    .desc = Inflicts 30 stamina damage on a borg or a genuine mindshield wearer, subject to resistance. One activation does not guarantee a stun.
    .suffix = Fish

ent-FishActionNetrunnerThermalSurge = thermal surge
    .desc = Raises a borg's or genuine mindshield wearer's temperature by up to 6 degrees, subject to heat resistance. Repeated use increases overheating risk.
    .suffix = Fish

ent-FishActionNetrunnerBatteryDrain = battery drain
    .desc = Removes up to 10 percent of a borg's or energy weapon's full battery capacity. Target the gun or its holder with the gun in the active hand; conventional firearms are incompatible.
    .suffix = Fish

ent-FishActionNetrunnerLockAirlock = hold airlock
    .desc = Prevents a closed, powered, unbolted airlock from opening for 12 seconds. Does not change bolts or close an already open passage.
    .suffix = Fish

ent-FishActionNetrunnerEraseTrace = buffer purge
    .desc = Clears a visible device's netrunner diagnostic records. The operator's journal and server audit remain intact.
    .suffix = Fish

fish-netrunner-manual-content =
    Thank you for purchasing the new Neural Terminal kit from Cybersun Industries LLC!

    Operating instructions:

    1. Contents and installation
    The kit contains an implanter, a neural interface machine board, a corporate jacket (delivered separately from the box) and this document. Metal, cable, manipulators and power must be obtained separately. Install the implant using the supplied implanter, then open the neural terminal action. A new implant has no purchased scripts. It receives 12 neural links once. The Script shop button opens the program and expansion catalog. Purchasing a script grants its action.

    2. Hardware expansions
    Three memory expansions each add 2 RAM, for a maximum capacity of 18 RAM. Three activation accelerators each remove 10 percent of the original activation time, up to 30 percent. Successive upgrades of the same type cost 6, 10 and 14 links. Added memory starts empty and fills during recovery. Acceleration does not reduce RAM costs, trace or cooldowns. Neural overload always requires at least 6 seconds to activate.
    The netrunner visor costs 30 links. When worn in the eyes slot, it divides script and global cooldowns by 1.5 while preserving memory costs and activation times. It doubles body heating per activation and displays genuine mindshields. One implant permits one visor order. Removing it disables acceleration; network disruption temporarily disables the visor.

    3. Local operation
    Select a purchased script and a visible compatible target. Infrastructure scripts have a 6 metre range and most combat scripts a 5 metre range. Area blackout requires a visible center within 6 metres. Walls and closed obstructions prevent local connections. Movement or damage to the operator interrupts activation. Only one script may activate at a time. Baseline memory recovery is 1 RAM every 10 seconds, beginning 8 seconds after operation.

    4. Neural interface
    Assemble a machine frame with the supplied board, 5 units of steel, 5 cable and 2 manipulators. Anchor the machine and connect it to power. Lie in the chair. Connection takes 5 seconds and consumes 1 RAM. The terminal selects the nearest available camera automatically.
    Ordinary movement controls move the observation channel through physical walls. Only functioning cameras on the chair's station supply an image. Uncovered areas, disabled cameras and other stations provide no vision. Selecting a camera moves the channel to that camera. Remote scripts require camera coverage and their normal range. Moving the channel does not interrupt activation.
    Closing the terminal window preserves the connection. Use Disconnect or release the chair restraints to leave. Power failure, implant extraction, loss of consciousness, death or session termination disconnect the operator.

    5. Cooling and neural links
    A powered chair restores 0.4 RAM per second after the recovery delay. Connection upkeep consumes 1 RAM every 20 seconds. Trace decays by 0.3 percentage points per second in the chair instead of the baseline 0.1. Insufficient upkeep memory causes disconnection.
    Each newly hacked device grants 1 neural link once. Repeated interference with the same device earns nothing. The chair compiles 1 link per 60 seconds of uninterrupted quiet operation at no more than 30 percent trace. Script activation or higher trace restarts compilation. Passive production is limited to 20 links per implant. Credits, purchased programs and hardware upgrades survive extraction and reinstallation.

    6. Script compatibility
    Open airlock performs one opening cycle on a powered, unbolted airlock. Bolts and physical locks are not bypassed. Camera disruption lasts 20 seconds. Area blackout suppresses powered lights within a 7 metre radius for 5 minutes; remote execution only affects camera covered lights. Worn suit sensor suppression lasts 20 seconds and headset transmission and reception suppression 15 seconds. Original device settings are preserved.
    Short circuit inflicts electrical damage on a borg, subject to chassis protection. Motor interference slows a borg or genuine mindshield implant wearer by 35 percent for 7 seconds. Optical glitch obscures borg optics or a worn electronic HUD visor for 3 seconds. Removing the visor restores sight. Energy weapon lock prevents firing for 6 seconds. Target the gun or a person holding it in their active hand. Conventional firearms are incompatible.
    Neural overload requires a genuine mindshield implant. Fake implants and unimplanted targets are incompatible. It paralyzes the target for 6 seconds, costs 10 RAM, generates 90 percent trace and has a 180 second cooldown. It transmits no target or security warning and shows no connection or stun indicator. Physiological consequences remain observable.
    Grip shock makes a borg or genuine mindshield wearer drop their active item. Neural static inflicts 30 stamina damage on compatible targets, subject to resistance. Thermal surge raises their temperature by up to 6 degrees, subject to heat protection. Battery drain removes up to 10 percent of a borg's or energy weapon's full battery capacity. Targeting a person requires a compatible gun in their active hand.
    Hold airlock prevents a closed, powered, unbolted airlock from opening for 12 seconds without changing its bolts. Buffer purge clears the selected device's interference diagnostics while retaining the operator's journal and server audit. Each airlock netrunner record expires automatically after 10 minutes.

    7. Operating risks
    The operator's body remains in the chair. Observation does not protect it from attack, suffocation, bleeding or environmental hazards. The chair is neither a medical device nor a stasis unit.
    Every activation heats the body in proportion to memory cost: 0.18 degrees per RAM normally and 0.36 with the visor. Interruption does not undo heat already applied. Monitor terminal thermometry and take breaks under high load. Faster chair memory recovery does not replace medical body cooling.
    Some scripts permit consumption of up to 4 RAM beyond available memory. Memory deficit and rapid repeated execution cause bleeding, temporary paralysis and additional trace. New scripts cannot start while RAM is negative. A memory reserve is recommended.
    Activation immediately generates partial trace even if interrupted. Interrupted neural overload also consumes half its RAM cost. Interference is recorded in terminal and device diagnostics; neural overload leaves no public target record. Trace thresholds of 30, 60 and 90 percent may alert security to the operator's or chair's approximate sector, except during neural overload. Extraction does not clear logs, trace or cooldowns.

    8. Terms of use
    The operator must supply power, physical protection and compatible targets. Cybersun Industries LLC does not guarantee concealment when combat scripts are used and does not compensate for operation beyond rated load. Store this document separately from working logs.
