## Borg Mk2 (borgType: FishGenericMk2, FishEngineeringMk2, FishMiningMk2, FishJanitorMk2, FishMedicalMk2,
## FishServiceMk2, FishPeaceMk2, FishClownMk2, FishSecurityMk2). Скрыты из меню выбора специализации
## (hideInMenu), назначаются только через отдельный Mk2-chassis.
borg-type-fish-generic-mk2-name = { borg-type-generic-name } Мк2
borg-type-fish-generic-mk2-desc = { borg-type-generic-desc } Усиленная версия Мк2.
borg-type-fish-generic-mk2-transponder = { borg-type-generic-transponder } Мк2

borg-type-fish-engineering-mk2-name = { borg-type-engineering-name } Мк2
borg-type-fish-engineering-mk2-desc = { borg-type-engineering-desc } Усиленная версия Мк2.
borg-type-fish-engineering-mk2-transponder = { borg-type-engineering-transponder } Мк2

borg-type-fish-mining-mk2-name = { borg-type-mining-name } Мк2
borg-type-fish-mining-mk2-desc = { borg-type-mining-desc } Усиленная версия Мк2.
borg-type-fish-mining-mk2-transponder = { borg-type-mining-transponder } Мк2

borg-type-fish-janitor-mk2-name = { borg-type-janitor-name } Мк2
borg-type-fish-janitor-mk2-desc = { borg-type-janitor-desc } Усиленная версия Мк2.
borg-type-fish-janitor-mk2-transponder = { borg-type-janitor-transponder } Мк2

borg-type-fish-medical-mk2-name = { borg-type-medical-name } Мк2
borg-type-fish-medical-mk2-desc = { borg-type-medical-desc } Усиленная версия Мк2.
borg-type-fish-medical-mk2-transponder = { borg-type-medical-transponder } Мк2

borg-type-fish-service-mk2-name = { borg-type-service-name } Мк2
borg-type-fish-service-mk2-desc = { borg-type-service-desc } Усиленная версия Мк2.
borg-type-fish-service-mk2-transponder = { borg-type-service-transponder } Мк2

borg-type-fish-peace-mk2-name = { borg-type-peace-name } Мк2
borg-type-fish-peace-mk2-desc = { borg-type-peace-desc } Усиленная версия Мк2.
borg-type-fish-peace-mk2-transponder = { borg-type-peace-transponder } Мк2

borg-type-fish-clown-mk2-name = { borg-type-clown-name } Мк2
borg-type-fish-clown-mk2-desc = { borg-type-clown-desc } Усиленная версия Мк2.
borg-type-fish-clown-mk2-transponder = { borg-type-clown-transponder } Мк2

## Охранник Mk2: описание собственное, без ссылки на родителя.
borg-type-fish-security-mk2-name = Охранник Мк2
borg-type-fish-security-mk2-desc = Усиленный вариант охранного киборга с обновлённой бронёй и внутренними системами.
borg-type-fish-security-mk2-transponder = киборг-охранник Мк2

## Borg Upgrade Module (предмет FishBorgUpgradeModule): отменяемый за 15 секунд
## апгрейд обычного киборга до его Mk2-варианта. Сущность не пересоздаётся — меняется только тип.
borg-upgrade-invalid-target = Этого киборга нельзя улучшить.
borg-upgrade-already-mk2 = Этот киборг уже Mk2.
borg-upgrade-in-progress = Улучшение уже выполняется.
borg-upgrade-complete = Киборг был улучшен до Mk2.
borg-upgrade-failed = Улучшение не удалось. Киборг остался без изменений.

## Действие отдыха (сущность FishBorgRestAction): переключает состояние отдыха боргов Mk2.
## Пока борг отдыхает: движение и все действия кроме этого заблокированы, расход батареи снижен.
ent-FishBorgRestAction = отдых
    .desc = Лечь и отдохнуть. Во время отдыха движение и действия заблокированы, а расход батареи снижен. OOC: Нажмите ещё раз, чтобы встать.
