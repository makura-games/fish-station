## Implanter Attempt Messages

implanter-component-implanting-target = { $user } пытается что-то в вас имплантировать!
implanter-component-implant-failed = { $implant } нельзя имплантировать в { $target }!
implanter-draw-failed-permanent = { $implant } вросся в { $target } и не может быть удалён!
implanter-draw-failed = Вы пытаетесь удалить имплант, но ничего не находите.
implanter-draw-failed-catastrophically = Имплантер ничего не находит и катастрофически отказывает, выбрасывая генетический материал в руку { $user }!
implanter-component-implant-already = { $target } уже имеет { $implant }!

## Extractor Messages
implanter-blocked-implant-stored = В этом экстракторе уже есть имплант. Извлеките его сначала, прежде чем извлекать другой.
implanter-extraction-failed-no-implants = У цели нет имплантов! Генетическая травма.
implanter-extraction-failed-not-found = У цели нет такого импланта! Генетическая травма.
implanter-extraction-success = Имплант успешно извлечён.
implanter-cannot-reimplant-extracted = Нельзя повторно внедрить извлечённый имплант. Сначала извлеките его из экстрактора.

## UI

implanter-set-draw-verb = Настроить извлечение импланта
implanter-set-draw-window = Настройка извлечения импланта
implanter-set-draw-info = Выберите тип импланта, который должен удалять этот имплантер:
implanter-set-draw-type = Тип импланта:

implanter-draw-text = Извлечение
implanter-inject-text = Установка

implanter-empty-text = Пусто

implanter-label-inject = [color=green]{ $implantName }[/color]
    Mode: [color=white]{ $modeString }[/color]
implanter-label-draw = [color=red]{ $implantName }[/color]
    Mode: [color=white]{ $modeString }[/color]
implanter-label = [color=green]{ $implantName }[/color]
    Режим: [color=white]{ $modeString }[/color]
implanter-label-blocked = [color=red]ЗАБЛОКИРОВАН[/color] - Содержит: { $implantName }

implanter-contained-implant-text = [color=green]{ $desc }[/color]

implanter-random-extract = Случайный имплант

implanter-implant-stored-blocked = Имплант внутри - экстрактор заблокирован

action-name-toggle-fake-mindshield = [color=green]Контроль защиты разума[/color]
action-description-toggle-fake-mindshield = Активирует/деактивирует передачу сигнала имитатора защиты разума

## Implanter Actions

scramble-implant-activated-popup = Вы превратились в { $identity }
deathrattle-implant-dead-message = Зафиксирована смерть { $user } { $position }.
deathrattle-implant-critical-message = Жизненные показатели { $user } критические, требуется немедленная помощь { $position }.

## Extractor Radial Menu
implanter-radial-mindshield = Имплант защиты разума
implanter-radial-tracking = Имплант-трекер
implanter-radial-random = Случайный имплант

implanter-label-extractor-ready = Готово: {$mode}
implanter-label-extractor-empty = Выберите режим извлечения