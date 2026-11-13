reagent-name-fish-detox = detoxol
reagent-desc-fish-detox = A medicine that reduces chemical dependence.
reagent-name-fish-deliriumine = deliriumine
reagent-desc-fish-deliriumine = A hallucinogen that causes strange effects and psychosis.
reagent-name-fish-antipsychotic = neurolith
reagent-desc-fish-antipsychotic = A medicine that reduces psychosis.
entity-effect-guidebook-fish-psychosis = { $chance ->
    [1] Causes
    *[other] has a { $chance } chance to cause
} { $amount } psychosis per unit
entity-effect-guidebook-fish-reduce-psychosis = { $chance ->
    [1] Reduces
    *[other] has a { $chance } chance to reduce
} psychosis by { $amount } per unit
entity-effect-guidebook-fish-restore-neuro = { $chance ->
    [1] Restores
    *[other] has a { $chance } chance to restore
} { $amount } neurons per unit
entity-effect-guidebook-fish-mental-health = { $chance ->
    [1] Affects
    *[other] has a { $chance } chance to affect
} stress ({ $stress }), neurons ({ $neuro }) and addiction ({ $addiction })
entity-effect-guidebook-fish-detox = { $chance ->
    [1] Reduces
    *[other] has a { $chance } chance to reduce
} addiction by { $amount } per unit
fish-health-analyzer-neuro-level = Neurons
fish-neuro-muddling = Your mind feels clouded.
fish-neuro-critical = Your mind is collapsing.
ent-FishNeuroCriticalSlowdownStatusEffect = neuro critical slowdown
    .desc = Your movements are severely impaired.
    .suffix = Fish
ent-FishNeuroModerateSlowdownStatusEffect = neuro moderate slowdown
    .desc = Your movements are slowed.
    .suffix = Fish
ent-FishNeuroMuddledStatusEffect = neuro muddling
    .desc = Your vision is blurred and your thoughts are clouded.
    .suffix = Fish
