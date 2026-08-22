# Аудит: Battle Shuttle (исторический, до lean-рефакторинга)

Дата: 2026-07-25 (архив)  
Ветка: `feature/space-battle-shuttle`  
PR: https://github.com/space-sunrise/fish-station/pull/319

Документ фиксирует состояние **раннего** слоя до data-driven рефакторинга в нативную архитектуру Fish Station.  
Актуальная правда: `BattleShuttles.md` + `BattleShuttles-Architecture.md`.

## 1. Что было

Первая итерация жила на стеке `BaseMechPod` под ранними внутренними именами домена, затем переименована в **Battle Shuttle**.

Поведение уже опиралось на Mech (посадка, батарея, equipment, BUI), но доменные слоты и модули были жёстко зашиты.

## 2. Content vs Engine

- вся логика — **Content**;
- полёт через Mech (`KinematicController`, `MovementAlwaysTouching`);
- **изменения Engine не нужны**.

## 3. Проблемы первой итерации

1. Внутренние имена не совпадали с продуктовым доменом Fish (**Battle Shuttle**).
2. Слоты enum — новый слот = правка C#.
3. Ore scoop и константы захардкожены в system.
4. Плоское Prototype-дерево, дубли Sprite/Mech states.
5. Placeholder-маркеры без поведения.

## 4. Что сохранили

- Опора на Mech BUI / equipment / battery / cabin air.
- Код в `_Fish`, тонкие hooks где нужно.
- Боевые статы оружия в YAML.
- Health / density / maxEquipmentAmount через Prototype.

## 5. Итог рефакторинга

Выполнено: домен **BattleShuttle**, data-driven модули, иерархия классов Prototype, lean Shared/Server слой, русская документация, интеграционные тесты.

## 6. Нецели

- MapGrid-shuttle thrusters.
- Профессия пилота / loadout / антаг.
- Правки RobustToolbox.
