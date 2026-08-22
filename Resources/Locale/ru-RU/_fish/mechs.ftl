mech-internal-damage-applied = Внутренние системы повреждены!
mech-internal-damage-repaired-power = Силовая шина стабилизирована.
mech-internal-damage-repaired-hull = Гермокорпус / термоконтур восстановлены.
mech-internal-damage-repaired-drive = Привод разблокирован.

mech-facing-armor-deflect = Удар сорвался с брони!

mech-overload-on = Форсаж привода включён.
mech-overload-off = Форсаж привода выключен.
mech-overload-too-damaged = Корпус слишком повреждён для форсажа.

mech-defence-on = Оборонительный режим включён.
mech-defence-off = Оборонительный режим выключен.

mech-thrusters-on = Маневровые двигатели включены.
mech-thrusters-off = Маневровые двигатели выключены.

mech-smoke-launched = Дымовая завеса! Осталось зарядов: {$charges}.
mech-smoke-empty = Дымовые заряды закончились.
mech-smoke-cooldown = Дымовая система перезаряжается.
mech-smoke-failed = Нельзя развернуть дым здесь.

mech-strafe-on = Боковое скольжение включено.
mech-strafe-off = Боковое скольжение выключено.

mech-equipment-swap-popup = Основной модуль: {$item}
mech-equipment-swap-none-popup = Основной модуль: кулаки

mech-dna-lock-set = Биометрический замок установлен.
mech-dna-lock-cleared = Биометрический замок снят.
mech-dna-lock-denied = Биометрия не совпадает — вход запрещён.
mech-dna-lock-no-dna = Нет биометрии для замка.

mech-maint-ready = Сервисный режим выключен — шасси готово.
mech-maint-service-hold = Сервисный холд: движение заблокировано.
mech-maint-access-panel = Сервисная панель открыта.
mech-maint-blocks-equipment = Нельзя ставить модули вне штатного режима.

mech-ui-status-ok = Системы в норме.
mech-ui-internal-damage = Внутренние отказы: {$flags}
mech-ui-overload-active = Форсаж ВКЛ
mech-ui-defence-active = Оборона ВКЛ
mech-ui-thrusters-active = Маневры ВКЛ
mech-ui-strafe-active = Скольжение ВКЛ
mech-ui-dna-locked = Биозамок активен
mech-ui-maintenance = Сервис: {$state}

ent-MechOdysseus = одиссей
    .desc = Медицинский экзоскелет для эвакуации и стабилизации пациентов.

ent-MechOdysseusBattery = одиссей
    .suffix = Батарея
    .desc = Медицинский экзоскелет для эвакуации и стабилизации пациентов.

ent-MechOdysseusFilled = одиссей
    .suffix = Заполненный
    .desc = Медицинский экзоскелет для эвакуации и стабилизации пациентов.

ent-MechEquipmentSleeper = бортовой слипер
    .desc = Медицинский модуль-слипер для удержания и стабилизации пациента.

ent-MechEquipmentRescueJaw = спасательные клещи
    .desc = Гидравлические клещи для вскрытия дверей и расчистки путей при спасении.

ent-MechEquipmentSyringeGun = шприцемёт меха
    .desc = Бортовой пневматический шприцемёт для быстрой доставки реагентов.

mech-internals-on = Кабинный резерв воздуха включён.
mech-internals-off = Кабинный резерв воздуха выключен.
mech-radio-mic-on = Радиомикрофон включён.
mech-radio-mic-off = Радиомикрофон выключен.
mech-radio-speaker-on = Радиодинамик включён.
mech-radio-speaker-off = Радиодинамик выключен.
mech-zoom-on = Оптический зум включён — движение заблокировано.
mech-zoom-off = Оптический зум выключен.
mech-phasing-on = Фазовый режим включён.
mech-phasing-off = Фазовый режим выключен.
mech-damtype-cycled = Тип рукопашного урона: {$type}
mech-wreckage-empty = Больше нечего извлечь.
mech-wreckage-salvaged = Из обломков извлечено оборудование.
mech-wreckage-scrap = Из обломков извлечён металлолом.
mech-ui-internals-on = Кабинный воздух: ВКЛ
mech-ui-internals-off = Кабинный воздух: ВЫКЛ
mech-ui-zoom-active = Зум ВКЛ
mech-ui-phasing-active = Фаза ВКЛ
mech-tracking-title = Трекинг мехов
mech-tracking-refresh = Обновить
mech-tracking-no-pilot = (пусто)
mech-tracking-broken = СЛОМАН
mech-tracking-ok = ОК
mech-tracking-entry = {$name} | корпус {$integrity}% | энергия {$energy}% | пилот {$pilot} | {$status}

mech-sleeper-patient = Пациент: {$name}
mech-sleeper-patient-empty = Пациент: нет
mech-sleeper-patient-unknown = неизвестен
mech-sleeper-eject = Извлечь пациента
mech-sleeper-reagents-header = Реагенты для инъекции
mech-sleeper-inject-hint = Клик по реагенту вводит {$amount} ед.
mech-sleeper-reagent-entry = {$name} ({$quantity})
mech-sleeper-no-patient = В слипере нет пациента.
mech-sleeper-no-reagents = Нет доступных реагентов.
mech-sleeper-inject-failed = Не удалось ввести реагент.
mech-sleeper-injected = Введено {$amount} ед. {$reagent} пациенту {$patient}.

ent-MechWreckage = обломки меха
    .desc = Искалеченные останки экзоскелета. Лом может извлечь части.

ent-MechBayPad = зарядная площадка мехов
    .desc = Пол, заряжающий силовые клетки припаркованных экзоскелетов.

ent-ComputerMechTracking = консоль трекинга мехов
    .desc = Отслеживает маяки зарегистрированных экзоскелетов на станции.

ent-MechTrackingComputerCircuitboard = плата консоли трекинга мехов
    .desc = Печатная плата для консоли трекинга мехов.
