# Архитектурное решение: Battle Shuttle как специализация Mech

Дата: 2026-07-26  
Ветка: `feature/space-battle-shuttle`  
PR: https://github.com/space-sunrise/fish-station/pull/319

## Требования (только поведение)

1. Герметичный EVA-аппарат с одним пилотом.
2. Классы (light/medium/heavy/experimental/civilian): прочность, скорость, масса, вместимость модулей.
3. Ключевой замок: блокирует посадку; импринт ключа; lock buster.
4. Сервисный люк: открытие ломом; закрытый люк блокирует установку модулей игроком; startingEquipment допускается при закрытом люке.
5. Модули: уникальные слоты, совместимость с классами, модификаторы скорости/массы/пассажиров.
6. Оружие питается от батареи меха; карго/ore scoop; locator; пассажирский Strap.
7. Урон, EMP, воздух, свет, BUI, ввод — как у меха.

## Решение

**Не** параллельный vehicle-стек. **Не** MapGrid-shuttle.  
Battle Shuttle = YAML-подклассы `BaseMechPod` + минимальный Content-слой поверх Mech ECS.

### Переиспользуем Mech

Entry/exit, equipment container, battery, BUI, actions, appearance Open/Broken, damage, EMP, air, guns energy, whitelist, startingEquipment, movement.

### Собственный код только для

| Компонент | Роль |
| --- | --- |
| `BattleShuttle` | Unlocked, HatchOpen, lock state, density base, passenger base, classTags |
| `BattleShuttleModule` | Slot, OccupantMod, Mass/Walk/Sprint, CompatibleShuttleTags, Cost (каталог) |
| `BattleShuttleLock` / `Key` / `LockBuster` | Ключевой замок |
| `BattleShuttleOreScoop` | Автосбор по тегу при движении |

Одна пара Shared/Server systems. Client appearance override **удаляется**: визуал кабины = Mech empty/occupied; люк — логическое состояние обслуживания.

### Правила взаимодействия

- Лом + **закрытая** wires panel → toggle hatch.
- Лом + **открытая** wires panel → Mech battery pry (не перехватываем).
- Установка модуля: `AfterInteract` на equipment **before** `MechEquipmentSystem` проверяет люк/слот/совместимость.

### Content vs Engine

Всё в Content. RobustToolbox не трогаем.

### Производительность

- Ore scoop: флаг `HasActiveOreScoop` на шаттле, без поиска модулей каждый MoveEvent.
- Без LINQ в hot path.
- Lookup с переиспользуемым буфером где возможно.
