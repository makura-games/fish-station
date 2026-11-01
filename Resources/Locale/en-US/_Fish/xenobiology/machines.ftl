fish-xeno-processor-alive = That slime is still alive!
fish-xeno-processor-done = The processor yields {$count} extract(s).
fish-xeno-recycler-alive = Only dead monkeys can be recycled.
fish-xeno-recycler-progress = Recycler: {$stored}/{$need} monkeys processed.
fish-xeno-recycler-done = A monkey cube pops out.

fish-xeno-console-title = Xenobiology console
fish-xeno-console-status-idle = Ready.
fish-xeno-console-stored = Stored slimes: {$count}/{$max}
fish-xeno-console-cubes = Monkey cubes in storage: {$count}
fish-xeno-console-potion = Loaded potion: {$name}
fish-xeno-console-no-potion-slot = No potion loaded (click with a slime potion).
fish-xeno-console-btn-collect = Collect slimes in range
fish-xeno-console-btn-release = Release all slimes
fish-xeno-console-btn-scan = Scan stored slimes
fish-xeno-console-btn-feed = Feed loaded potion
fish-xeno-console-btn-monkey = Release monkey (1 cube)
fish-xeno-console-btn-recycle = Recycle dead monkeys in range
fish-xeno-console-access = Access denied.
fish-xeno-console-unpowered = Console has no power.
fish-xeno-console-cube-added = Monkey cube stored in console.
fish-xeno-console-potion-loaded = Potion loaded into console.
fish-xeno-console-released = Slimes released.
fish-xeno-console-collected = Collected {$count} slime(s).
fish-xeno-console-no-cubes = No monkey cubes stored.
fish-xeno-console-monkey-out = Monkey released.
fish-xeno-console-no-slimes = No slimes in storage.
fish-xeno-console-scanned = Scan complete.
fish-xeno-console-no-potion = No potion loaded.
fish-xeno-console-fed = Potion fed to a slime.
fish-xeno-console-feed-fail = Could not feed any stored slime.
fish-xeno-console-recycled = Recycled {$count} monkey corpse(s).

ent-FishXenobiologyConsoleFish = xenobiology console
    .desc = Stores slimes and monkey cubes; feeds potions in a radius around the console.
    .suffix = Fish

ent-FishSlimeProcessorFish = slime processor
    .desc = Grinds dead slimes into slime extracts equal to their core count.
    .suffix = Fish

ent-FishMonkeyRecyclerFish = monkey recycler
    .desc = Processes five dead monkeys into one monkey cube.
    .suffix = Fish

ent-FishXenobiologySlimeScannerFish = slime scanner
    .desc = Reads nutrition, growth, mutation, cores, and power of a slime.
    .suffix = Fish
