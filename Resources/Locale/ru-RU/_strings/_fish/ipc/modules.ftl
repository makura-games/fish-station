ent-FishBaseIpcModule = модуль шасси КПБ
    .desc = Модуль апгрейда кибернетического позитронного блока.
    .suffix = Fish

ent-FishIpcModuleWireInterface = модуль интерфейса проводов КПБ
    .desc = Диагностический интерфейс подсвечивает проводку шасси. Почти не ест заряд, но занимает слот.
    .suffix = Fish

ent-FishIpcModuleApcAdapter = модуль адаптера ЛКП КПБ
    .desc = Ускоряет зарядку шасси от ЛКП и совместимых источников. Батарея всё равно конечная.
    .suffix = Fish

ent-FishIpcModuleVocalMod = модуль вокального модулятора КПБ
    .desc = Держит встроенную рацию в эфире даже при просадке заряда.
    .suffix = Fish

ent-FishIpcModuleToolHand = модуль инструмент-руки КПБ
    .desc = Встраивает один лом в свободную руку. Это не полный набор инструментов.
    .suffix = Fish

ent-FishIpcModuleMedHud = модуль медицинской оптики КПБ
    .desc = Встроенный мед-HUD. Занимает слот зрения.
    .suffix = Fish

ent-FishIpcModuleSecHud = модуль охранной оптики КПБ
    .desc = Встроенный сек-HUD. Занимает слот зрения.
    .suffix = Fish

ent-FishIpcModuleMeson = модуль мезонной оптики КПБ
    .desc = Встроенная защита глаз от сварки. Занимает слот зрения.
    .suffix = Fish

ent-FishIpcModuleNightVision = модуль ночного зрения КПБ
    .desc = Оптика низкого освещения для шасси. Занимает слот зрения.
    .suffix = Fish

ent-FishIpcModuleNanoRepair = модуль наноремонта КПБ
    .desc = Микродроны медленно чинят корпус, жадно расходуя батарею.
    .suffix = Fish

ent-FishIpcModuleReactiveRepair = модуль реактивного ремонта КПБ
    .desc = Чинит быстрее наноремонта и ещё жаднее. Взаимоисключает с наноремонтом.
    .suffix = Fish

ent-FishIpcModuleEmpScreen = модуль EMP-экрана КПБ
    .desc = Частичная защита шасси от ЭМИ. Не полный иммунитет.
    .suffix = Fish

ent-FishIpcModuleMagneticJoints = модуль магнитных суставов КПБ
    .desc = Магнитные крепления сопротивляются ампутации. EMP временно отключает эффект.
    .suffix = Fish

ent-FishIpcModuleSealedJoints = модуль герметичных суставов КПБ
    .desc = Герметичные крепления блокируют ампутацию. EMP жёстко стопорит шасси. Взаимоисключает с магнитными.
    .suffix = Fish

ent-FishIpcModuleJumpstarter = модуль jumpstarter КПБ
    .desc = Разряд конденсаторов вытаскивает шасси из крита — не из смерти. Долгий КД.
    .suffix = Fish

ent-FishIpcModuleDefib = модуль дефибриллятора КПБ
    .desc = Встроенные дефиб-накладки. Каждый разряд съедает огромный кусок батареи.
    .suffix = Fish

ent-FishIpcModuleRadFilter = модуль радфильтра КПБ
    .desc = Режет радиацию и связанный клеточный урон ценой постоянного расхода.
    .suffix = Fish

ent-FishActionIpcDefib = дефибриллятор шасси
    .desc = Разрядить конденсаторы шасси в пациента. Огромный расход батареи.

ent-FishActionIpcJumpstarter = импульс jumpstarter
    .desc = Сбросить заряд конденсаторов, чтобы вырваться из критического состояния.

fish-ipc-module-panel-closed = Сначала откройте панель шасси.
fish-ipc-module-locked = Корпус заблокирован.
fish-ipc-module-too-many = В шасси уже максимум модулей ({ $max }).
fish-ipc-module-whitelist-deny = Этот модуль не подходит к шасси КПБ.
fish-ipc-module-exclusion = Модуль этой группы уже установлен.
fish-ipc-module-eject = Извлечь { $module }
fish-ipc-module-need-charge = Недостаточно заряда батареи.
fish-ipc-module-cooldown = Конденсаторы модуля ещё перезаряжаются.
fish-ipc-module-defib-self = Нельзя дефибриллировать себя накладками шасси.
fish-ipc-module-defib-success = Разряд стабилизирует { $target }.
fish-ipc-module-defib-fail = Разряд не оживляет { $target }.
fish-ipc-module-jumpstarter-not-crit = Jumpstarter работает только в критическом состоянии.
fish-ipc-module-jumpstarter-success = Импульс jumpstarter поднимает шасси.
fish-ipc-module-jumpstarter-fail = Импульса мало — урон всё ещё смертельный.
fish-ipc-module-tool-no-hand = Нет свободной руки для встроенного инструмента.
fish-ipc-module-wire-interface-self = Интерфейс проводов активен: панели рядом дают доп. диагностику.
fish-ipc-module-wire-interface-panel-open = Интерфейс проводов: панель открыта.
fish-ipc-module-wire-interface-panel-closed = Интерфейс проводов: панель закрыта.
fish-ipc-module-joints-sealed-block = Герметичные суставы не поддаются.
fish-ipc-module-joints-magnetic-resist = Магнитные крепления возвращают конечность на место!
fish-ipc-module-joints-emp-magnetic = EMP выбивает магнитные суставы!
fish-ipc-module-joints-emp-sealed = EMP стопорит герметичные суставы — шасси клинит!
fish-ipc-emp-hit-screened = EMP бьёт по шасси, но экран снимает большую часть удара.

research-technology-fish-ipc-wire-interface = Интерфейс проводов КПБ
research-technology-fish-ipc-apc-adapter = Адаптер ЛКП КПБ
research-technology-fish-ipc-vocal-mod = Вокальный модулятор КПБ
research-technology-fish-ipc-tool-hand = Инструмент-рука КПБ
research-technology-fish-ipc-med-hud = Медицинская оптика КПБ
research-technology-fish-ipc-sec-hud = Охранная оптика КПБ
research-technology-fish-ipc-meson = Мезонная оптика КПБ
research-technology-fish-ipc-night-vision = Ночное зрение КПБ
research-technology-fish-ipc-nano-repair = Наноремонт КПБ
research-technology-fish-ipc-reactive-repair = Реактивный ремонт КПБ
research-technology-fish-ipc-emp-screen = EMP-экран КПБ
research-technology-fish-ipc-magnetic-joints = Магнитные суставы КПБ
research-technology-fish-ipc-sealed-joints = Герметичные суставы КПБ
research-technology-fish-ipc-jumpstarter = Jumpstarter КПБ
research-technology-fish-ipc-defib = Дефибриллятор КПБ
research-technology-fish-ipc-rad-filter = Радфильтр КПБ
