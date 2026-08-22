# Архитектурное решение: Fish Mech

Дата: 2026-07-26  
Ветка: `feature/space-battle-shuttle`  
PR: https://github.com/space-sunrise/fish-station/pull/319

См. также: [`Mechs.md`](Mechs.md) (полная документация).

## Требования (поведение)

1. Меха — пилотируемая машина с батареей, модулями, BUI, уроном, светом, воздухом.
2. Внутренние отказы с эффектами и путями ремонта.
3. Броня зависит от сектора удара; возможен рикошет.
4. Два «ручных» слота (primary/secondary) при Sunrise radial.
5. Шасси-способности: форсаж, оборона, маневры, дым, скольжение, зум, фаза.
6. Биозамок и сервисный холд блокируют вход/движение/equip.
7. Медицинский Odysseus + medical modules.
8. Кабина: резерв воздуха, радио; станция: bay, tracking, wreckage.
9. Совместимость с Battle Shuttle (оба — Mech specialization).

## Решение

**Не** создавать параллельный vehicle/mecha стек.  
Собственные имена компонентов, UI-ключей и сервисных стадий Fish.  
**Да** — data-driven Content-слой `_Fish/Mechs` с собственной сервисной и броневой моделью.

### Переиспользуем

Mech entry/exit, equipment, battery, BUI, actions, guns, grabber, Sunrise select/paint/EMP, Battle Shuttle lock/hatch (отдельная специализация).

### Собственный код

См. таблицу в [`Mechs.md`](Mechs.md). Ключевые отличия дизайна:

- сервис: `Ready` / `ServiceHold` / `AccessPanel`;
- броня: абсолютные мультипликаторы и шансы + конусы 50°;
- оборона: резист урона, не бонус deflect;
- отказы: `CabinFire` / `CoolantFail` / `PowerSpike` / `DriveFault` / `HullBreach`.

### Правила интеграции

- Dual-hand: primary = `CurrentSelectedEquipment`.
- Facing: `DamageModifyEvent` + сектор по world rotation.
- Install gate: `MechEquipmentInstallGate` + `before: MechEquipmentSystem`.
- Прототипы: `_Fish` parents + `# Fish edit` на шасси.

### Content vs Engine

Только Content.

### Статус

Ядро и расширенный слой — **готово**. Тесты: `FishMechSystemsTest`.
