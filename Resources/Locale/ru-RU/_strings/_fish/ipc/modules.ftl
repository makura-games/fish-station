ent-FishBaseIpcModule = модуль шасси КПБ
    .desc = Платка апгрейда: вставляется в открытый корпус КПБ.
    .suffix = Fish

ent-FishIpcModuleWireInterface = модуль интерфейса проводов КПБ
    .desc = При осмотре панели видно, открыта она или нет. Чуть сажает батарею. Можно выключить.
    .suffix = Fish

ent-FishIpcModuleApcAdapter = модуль адаптера ЛКП КПБ
    .desc = Быстрее заряжается от ЛКП. Чуть сажает батарею. Можно выключить.
    .suffix = Fish

ent-FishIpcModuleVocalMod = модуль вокального модулятора КПБ
    .desc = Встроенная рация работает даже при севшей батарее. Можно выключить.
    .suffix = Fish

ent-FishIpcModuleToolHand = модуль руки-мультитула КПБ
    .desc = Пока включён — в свободную руку вылезает мультитул. Можно выключить.
    .suffix = Fish

ent-FishIpcModuleMedHud = модуль медицинской оптики КПБ
    .desc = Полоски и иконки здоровья поверх зрения. Можно выключить.
    .suffix = Fish

ent-FishIpcModuleSecHud = модуль охранной оптики КПБ
    .desc = Должность, майндшилд и криминальный статус поверх зрения. Можно выключить.
    .suffix = Fish

ent-FishIpcModuleMeson = модуль мезонной оптики КПБ
    .desc = Пока включён — глаза не слепнут от сварки. Стены не показывает, от вспышек не спасает. Можно выключить.
    .suffix = Fish

ent-FishIpcModuleNightVision = модуль термального зрения КПБ
    .desc = Тепловая картинка окружения. Жрёт батарею сильнее обычной оптики. Можно выключить.
    .suffix = Fish

ent-FishIpcModuleNanoRepair = модуль физического ремонта КПБ
    .desc = Медленно чинит удары, порезы и проколы. Не ставится вместе с термическим ремонтом. Можно выключить.
    .suffix = Fish

ent-FishIpcModuleReactiveRepair = модуль термического ремонта КПБ
    .desc = Медленно чинит жар и ток. Не ставится вместе с физическим ремонтом. Можно выключить.
    .suffix = Fish

ent-FishIpcModuleEmpScreen = модуль EMP-экрана КПБ
    .desc = Сильно снижает урон от ЭМИ, шасси не уходит в полный отклюк. Можно выключить.
    .suffix = Fish

ent-FishIpcModuleJumpstarter = модуль jumpstarter КПБ
    .desc = В крите даёт импульс «встать», дорого по батарее и с откатом. Смертельный урон не снимает. Можно выключить.
    .suffix = Fish

ent-FishIpcModuleDefib = модуль дефибриллятора КПБ
    .desc = Накладки в обе руки: разряд по другим. Себя бить нельзя. Жрёт кусок батареи. Можно выключить.
    .suffix = Fish

ent-FishIpcModuleRadFilter = модуль радфильтра КПБ
    .desc = Меньше радиации и клеточного урона. Без него шасси получает оба полностью. Можно выключить.
    .suffix = Fish

ent-FishActionIpcDefib = накладки дефибриллятора шасси
    .desc = Выпустить встроенные накладки дефиба в обе руки. Каждый разряд жрёт батарею.

ent-FishActionIpcJumpstarter = импульс jumpstarter
    .desc = Сбросить заряд конденсаторов, чтобы вырваться из критического состояния.

ent-FishActionIpcToggleRepair = переключить ремонт шасси
    .desc = Включить или выключить установленный модуль ремонта.

ent-FishActionIpcToggleVision = переключить оптику шасси
    .desc = Включить или выключить установленный модуль зрения.

ent-FishActionIpcToggleThermal = переключить термалы шасси
    .desc = Включить или выключить термальное зрение.

ent-FishActionIpcToggleMeson = переключить защиту сварки
    .desc = Включить или выключить защиту глаз от сварки.

ent-FishActionIpcToggleSec = переключить оптику СБ
    .desc = Включить или выключить очки службы безопасности.

ent-FishActionIpcToggleModule = переключить модуль шасси
    .desc = Включить или выключить модуль. Всплывёт статус.

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
fish-ipc-module-duplicate = Такой модуль уже установлен.
fish-ipc-module-eject = Извлечь { $module }
fish-ipc-module-need-charge = Недостаточно заряда батареи.
fish-ipc-module-cooldown = Конденсаторы модуля ещё перезаряжаются.
fish-ipc-module-defib-self = Нельзя дефибриллировать себя накладками шасси.
fish-ipc-module-defib-success = Разряд стабилизирует { $target }.
fish-ipc-module-defib-fail = Разряд не оживляет { $target }.
fish-ipc-module-jumpstarter-not-crit = Jumpstarter работает только в критическом состоянии.
fish-ipc-module-jumpstarter-success = Импульс jumpstarter поднимает шасси.
fish-ipc-module-jumpstarter-fail = Импульса мало — урон всё ещё смертельный.
fish-ipc-module-jumpstarter-still-crit = Импульса мало — шасси всё ещё в критическом состоянии.
fish-ipc-module-tool-no-hand = Нет свободной руки для встроенного инструмента.
fish-ipc-module-defib-hands = Для накладок нужны две свободные руки.
fish-ipc-module-wire-interface-self = Интерфейс проводов активен: панели рядом дают доп. диагностику.
fish-ipc-module-wire-interface-panel-open = Интерфейс проводов: панель открыта.
fish-ipc-module-wire-interface-panel-closed = Интерфейс проводов: панель закрыта.
fish-ipc-emp-hit-screened = EMP бьёт по шасси, но экран снимает большую часть удара.
fish-ipc-health-analyzer-refuse = Обычный медсканер не читает шасси КПБ.

research-technology-fish-ipc-healthcare = Модули здравоохранения КПБ
research-technology-fish-ipc-utility = Служебные модули КПБ
research-technology-fish-ipc-advanced-optics = Продвинутая оптика КПБ
