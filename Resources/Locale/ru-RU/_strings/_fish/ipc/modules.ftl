ent-FishBaseIpcModule = модуль шасси КПБ
    .desc = Модуль апгрейда кибернетического позитронного блока.
    .suffix = Fish

ent-FishIpcModuleWireInterface = модуль интерфейса проводов КПБ
    .desc = Расход 0,05 Дж/с. Осмотр панелей показывает, открыты они или закрыты. Выключается кнопкой.
    .suffix = Fish

ent-FishIpcModuleApcAdapter = модуль адаптера ЛКП КПБ
    .desc = Расход 0,1 Дж/с. Время зарядки от ЛКП ×0,55, порция за цикл прежняя. Выключается кнопкой.
    .suffix = Fish

ent-FishIpcModuleVocalMod = модуль вокального модулятора КПБ
    .desc = Расход 0,15 Дж/с. Встроенная рация работает даже при севшей батарее. Выключается кнопкой.
    .suffix = Fish

ent-FishIpcModuleToolHand = модуль инструмент-руки КПБ
    .desc = Расход 0,2 Дж/с. Пока включён, в свободную руку кладётся лом. Выключается кнопкой.
    .suffix = Fish

ent-FishIpcModuleMedHud = модуль медицинской оптики КПБ
    .desc = Расход 0,35 Дж/с. Слот зрения: полоски и иконки здоровья. Выключается кнопкой.
    .suffix = Fish

ent-FishIpcModuleSecHud = модуль охранной оптики КПБ
    .desc = Расход 0,35 Дж/с. Слот зрения: должность, майндшилд и криминальный статус. Выключается кнопкой.
    .suffix = Fish

ent-FishIpcModuleMeson = модуль мезонной оптики КПБ
    .desc = Расход 0,25 Дж/с. Слот зрения: добавляет защиту глаз от сварки. У шасси она уже есть. Выключается кнопкой.
    .suffix = Fish

ent-FishIpcModuleNightVision = модуль ночного зрения КПБ
    .desc = Расход 0,55 Дж/с. Слот зрения: зелёный фосфор и подъём освещения. Выключается кнопкой.
    .suffix = Fish

ent-FishIpcModuleNanoRepair = модуль наноремонта КПБ
    .desc = Расход 1,2 Дж/с, порог 10% заряда. Дробление, порез и прокол −0,15/с, жар и ток −0,2/с. Не вместе с реактивным ремонтом.
    .suffix = Fish

ent-FishIpcModuleReactiveRepair = модуль реактивного ремонта КПБ
    .desc = Расход 1,6 Дж/с, порог 12% заряда. Дробление, порез и прокол −0,35/с, жар и ток −0,4/с. Не вместе с наноремонтом.
    .suffix = Fish

ent-FishIpcModuleEmpScreen = модуль EMP-экрана КПБ
    .desc = Расход 0,45 Дж/с. Урон ЭМИ ×0,4, шасси не уходит в полный EMP-disable. Выключается кнопкой.
    .suffix = Fish

ent-FishIpcModuleMagneticJoints = модуль магнитных суставов КПБ
    .desc = Расход 0,4 Дж/с. 65% шанс отменить ампутацию. EMP глушит эффект. Не вместе с герметичными суставами.
    .suffix = Fish

ent-FishIpcModuleSealedJoints = модуль герметичных суставов КПБ
    .desc = Расход 0,5 Дж/с. Блокирует ампутацию. EMP оглушает на 12 с и глушит эффект. Не вместе с магнитными.
    .suffix = Fish

ent-FishIpcModuleJumpstarter = модуль jumpstarter КПБ
    .desc = Расход 0,2 Дж/с. Импульс только в крите: 30% заряда, откат 180 с, −15 дробящего и по −10 режущего, колющего, жара и тока. Смертельный урон не снимает. Выключается кнопкой.
    .suffix = Fish

ent-FishIpcModuleDefib = модуль дефибриллятора КПБ
    .desc = Расход 0,25 Дж/с. Накладки в обе руки. Разряд: 40% заряда, 4 с, откат 90 с, −40 удушья. Себя бить нельзя. Выключается кнопкой.
    .suffix = Fish

ent-FishIpcModuleRadFilter = модуль радфильтра КПБ
    .desc = Расход 0,6 Дж/с. Радиация и клеточный урон ×0,35. Без модуля шасси получает их полностью. Выключается кнопкой.
    .suffix = Fish

ent-FishActionIpcDefib = накладки дефибриллятора шасси
    .desc = Выпустить встроенные накладки дефиба в обе руки. Каждый разряд жрёт батарею.

ent-FishActionIpcJumpstarter = импульс jumpstarter
    .desc = Сбросить заряд конденсаторов, чтобы вырваться из критического состояния.

ent-FishActionIpcToggleRepair = переключить ремонт шасси
    .desc = Включить или выключить установленный модуль ремонта.

ent-FishActionIpcToggleVision = переключить оптику шасси
    .desc = Включить или выключить установленный модуль зрения.

ent-FishActionIpcToggleMagneticJoints = переключить магнитные суставы
    .desc = Включить или выключить сопротивление ампутации.

ent-FishActionIpcToggleSealedJoints = переключить герметичные суставы
    .desc = Включить или выключить блокировку ампутации.

ent-FishIpcDefibPads = накладки дефибриллятора шасси
    .desc = Встроенные шоковые накладки модуля КПБ.
    .suffix = Fish

fish-ipc-module-toggle-name = Переключить { $module }
fish-ipc-module-toggle-desc = Включить или выключить модуль. Выключенный не работает и не тратит заряд.
fish-ipc-module-toggled-off = Модуль выключен.
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
fish-ipc-health-analyzer-refuse = Обычный медсканер не читает шасси КПБ.

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
