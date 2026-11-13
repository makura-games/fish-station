fish-neuro-muddling = Ваше сознание мутится.
fish-neuro-critical = Разум разрушается.
fish-stress-critical = Паника нарастает.
fish-psychosis-onset = Вы чувствуете, что реальность начинает искажаться.
reagent-name-nootropil = ноотропил
reagent-desc-nootropil = Препарат для поддержки когнитивных функций.
reagent-name-fish-detox = детоксол
reagent-desc-fish-detox = Препарат для снижения химической зависимости.
reagent-name-fish-antipsychotic = нейролит
reagent-desc-fish-deliriumine = Галлюциногенный химикат, вызывающий потерю контроля над телом и психоз.
entity-effect-guidebook-fish-psychosis = { $chance ->
    [1] Вызывает
    *[other] с вероятностью { $chance } вызывает
} { $amount } психоза за единицу
entity-effect-guidebook-fish-reduce-psychosis = { $chance ->
    [1] Снижает
    *[other] с вероятностью { $chance } снижает
} психоз на { $amount } за единицу
entity-effect-guidebook-fish-restore-neuro = { $chance ->
    [1] Восстанавливает
    *[other] с вероятностью { $chance } восстанавливает
} { $amount } нейронов за единицу
entity-effect-guidebook-fish-mental-health = { $chance ->
    [1] Влияет
    *[other] с вероятностью { $chance } влияет
} на стресс ({ $stress }), нейроны ({ $neuro }) и зависимость ({ $addiction })
entity-effect-guidebook-fish-detox = { $chance ->
    [1] Снижает
    *[other] с вероятностью { $chance } снижает
} зависимость на { $amount } за единицу
reagent-desc-fish-antipsychotic = Препарат, снижающий уровень психоза.
fish-health-analyzer-neuro-level = Нейроны
ent-FishNeuroCriticalSlowdownStatusEffect = замедление при критическом уровне нейронов
    .desc = Ваши движения сильно затруднены.
    .suffix = Fish
ent-FishNeuroModerateSlowdownStatusEffect = замедление при низком уровне нейронов
    .desc = Ваши движения замедлены.
    .suffix = Fish
ent-FishNeuroMuddledStatusEffect = помутнение сознания
    .desc = Ваше зрение затуманено, мысли путаются.
    .suffix = Fish
