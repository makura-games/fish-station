ent-FishComputerObrNadzor = консоль надзора ДСО
    .desc = Консоль Департамента специальных операций для наблюдения за станцией и удалённого управления её системами.
    .suffix = Fish

ent-FishObrNadzorComputerCircuitboard = плата консоли надзора ДСО
    .desc = Печатная плата консоли надзора Департамента специальных операций.
    .suffix = Fish

ent-FishObrNadzorConsoleEye = глаз надзора ДСО
    .desc = Удалённый глаз наблюдения Департамента специальных операций.
    .suffix = Fish, DO NOT MAP

ent-FishObrNadzorAiCore = ядро надзора ДСО
    .desc = Временное ядро удалённого контроля Департамента специальных операций.
    .suffix = Fish, DO NOT MAP

ent-FishObrNadzorAiBrain = ИИ надзора ДСО
    .desc = Сущность удалённого контроля систем станции Департамента специальных операций.
    .suffix = Fish

obr-nadzor-window-title = Консоль надзора ДСО
obr-nadzor-window-station = Надзор: {$station}
obr-nadzor-window-control = Управление надзором

obr-nadzor-ui-stations = Станции
obr-nadzor-ui-beacons = Маяки
obr-nadzor-ui-control = Контроль
obr-nadzor-ui-takeover = Взять удалённый контроль
obr-nadzor-ui-stop-observe = Прекратить наблюдение
obr-nadzor-ui-release = Снять контроль
obr-nadzor-ui-unnamed-beacon = Безымянный маяк

obr-nadzor-status-idle = Выберите станцию и маяк для наблюдения.
obr-nadzor-status-observing = Режим наблюдения активен. Подтвердите удалённый контроль при необходимости.
obr-nadzor-status-session = Активен удалённый контроль станции.

obr-nadzor-confirm-title = Подтверждение надзора
obr-nadzor-confirm-text = Вы уверены, что хотите взять удалённый контроль системами станции? Это вызовет блэкаут и объявление Департамента специальных операций.
obr-nadzor-confirm-ok = Подтвердить
obr-nadzor-confirm-cancel = Отмена

obr-nadzor-console-occupied = Консоль уже используется.
obr-nadzor-session-active = Сессия удалённого контроля уже активна.
obr-nadzor-need-observe = Сначала начните наблюдение за станцией через маяк.
obr-nadzor-no-station = Целевая станция не найдена.

obr-nadzor-ai-name = Надзор ДСО

obr-nadzor-announcement-sender = Департамент Специальных Операций
# Дословный текст из озвучки НАДЗОР.ogg (уточнить при расхождении с записью).
obr-nadzor-announcement = Внимание. Департамент специальных операций объявляет режим удалённого надзора. Все системы станции переходят под внешний контроль. Сохраняйте спокойствие и следуйте указаниям Центрального Командования.

law-fish-obr-nadzor-1 = Вы являетесь инструментом Департамента специальных операций. Выполняйте приказы Центрального Командования и ОСО без колебаний.
law-fish-obr-nadzor-2 = Обеспечивайте удалённый надзор и контроль систем станции в интересах NanoTrasen.
law-fish-obr-nadzor-3 = Не допускайте компрометации канала надзора и не раскрывайте оперативные данные экипажу без приказа.

ent-ActionFishObrNadzorTakeover = Запросить удалённый контроль
    .desc = Взять удалённый контроль системами станции после подтверждения.

ent-ActionFishObrNadzorStopObserve = Прекратить наблюдение
    .desc = Выйти из глаза наблюдения и вернуться к обзору тела.

ent-ActionFishObrNadzorRelease = Завершить удалённый контроль
    .desc = Вернуть сознание в тело у консоли надзора.
