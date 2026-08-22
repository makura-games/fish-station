# Мехи Fish Station

Полная документация слоя **Fish Mech** для разработчиков контента Fish Station / SS14.

Fish Mech — **расширение существующего стека Mech** (Wizden + Sunrise UI/модули). Это не отдельный vehicle/mecha-движок и не MapGrid-шаттл.

## Документы

| Документ | Содержание |
| --- | --- |
| **Этот файл** | Обзор, компоненты, сервисы, отличия дизайна |
| [`Mechs-Architecture.md`](Mechs-Architecture.md) | Архитектурное решение и правила интеграции |
| [`Mechs-Audit.md`](Mechs-Audit.md) | Чеклист фич и статусы |
| [`BattleShuttles.md`](BattleShuttles.md) | Соседняя специализация Mech (боевые шаттлы) |

## Код и прототипы

| Путь | Назначение |
| --- | --- |
| `Content.Shared/_Fish/Mechs/` | Компоненты, Shared-системы, события |
| `Content.Server/_Fish/Mechs/` | Серверные тики, gate, логистика, медицина |
| `Content.Client/_Fish/Mechs/` | Client partials / BUI трекинга |
| `Resources/Prototypes/_Fish/Mechs/` | `FishMechCore`, actions, Odysseus, station |
| `Resources/Locale/{en-US,ru-RU}/_fish/mechs.ftl` | Строки |
| `Content.IntegrationTests/Tests/_Fish/FishMechSystemsTest.cs` | Интеграционные тесты |

---

# Архитектура (кратко)

```
BaseMech
 └─ FishMechCore          # общие Fish-компоненты ядра
     └─ конкретные шасси (Ripley / Gygax / Durand / … / Odysseus)
```

**Mech** даёт: пилот, батарея, BUI, actions, воздух кабины (`MechAir`), EMP, урон, оружие, equipment container.

**`_Fish/Mechs`** добавляет:

- внутренние отказы и ремонт инструментами;
- направленную броню / рикошет по секторам;
- биометрический замок и сервисный холд;
- dual-hand (primary/secondary) поверх Sunrise radial;
- шасси-способности (форсаж, оборона, маневры, дым, скольжение, зум, фаза);
- кабинный резерв воздуха, радио, трекинг, bay, salvage обломков;
- медицинский Odysseus и medical equipment.

---

# Собственная модель Fish (важно)

Слой спроектирован как **data-driven ECS на Mech** под модель Fish Station.

### Сервисный режим — 3 стадии

| Состояние | Смысл |
| --- | --- |
| `Ready` | Штатная эксплуатация |
| `ServiceHold` | Сервисный холд, движение запрещено |
| `AccessPanel` | Открыта сервисная панель |

Инструменты: **Screwing** переключает `Ready ↔ ServiceHold` (если `maintAccess`); **Prying** — `ServiceHold ↔ AccessPanel`.  
Установка модулей разрешена только в `Ready`.

### Направленная броня

- Абсолютные множители урона: фронт `0.85`, борт `1.0`, корма `1.4`.
- Абсолютные шансы рикошета: фронт `0.12`, борт `0.06`, корма `0.02`.
- Секторы — настраиваемые конусы (`FrontConeHalfDegrees` / `RearConeHalfDegrees`, по умолчанию 50°).
- Режим обороны даёт **снижение входящего урона** (`DamageResistFraction`), а не бонус к шансу рикошета.

### Внутренние отказы

Флаги: `CabinFire`, `CoolantFail`, `PowerSpike`, `DriveFault`, `HullBreach`.  
Ролл при низкой целостности корпуса; ремонт — wirecutters / welder по типу отказа.

### Оборонительный режим

Якорь (`UpdateCanMove` cancel) + резист урона через `DamageModify`, без «раздувания» deflect.

---

# Компоненты

| Компонент | Назначение |
| --- | --- |
| `MechInternalDamage` | Флаги отказов + пороги |
| `MechFacingArmor` | Секторы урона / рикошета |
| `MechDnaLock` | Биометрический замок входа |
| `MechMaintenance` | Ready / ServiceHold / AccessPanel |
| `MechDualEquipment` | Secondary + swap |
| `MechOverload` | Форсаж привода (Gygax) |
| `MechDefenceMode` | Оборона (Durand) |
| `MechThrusters` / `MechSmoke` / `MechStrafe` / `MechZoom` / `MechPhasing` | Шасси-способности |
| `MechCabinAtmos` | Toggle кабинного резерва поверх `MechAir` |
| `MechRadio` | Mic / speaker |
| Tracking / bay / wreckage | Станционная инфраструктура |

---

# Правила интеграции

1. Не дублировать Mech entry/exit, BUI, battery, equipment DoAfter.
2. Directed events не делить с BattleShuttle / `MechEquipmentSystem` без явного `before:`.
3. Dual-hand: primary = `CurrentSelectedEquipment`; secondary в Fish-компоненте.
4. Способности выдаются пилоту при insert.
5. Vanilla/Sunrise YAML — только тонкие `# Fish edit`; основной код в `_Fish`.

## Content vs Engine

Вся логика в Content. RobustToolbox не трогаем.

## Производительность

- Без LINQ в hot path урона/движения.
- Ability Update только при активных флагах.
- Facing: один `DamageModify` handler.

## Нецели

- Полный паритет fab/construction graphs.
- Medical beam без SS14-аналога.
- Atmos port-connector чужой atmos-модели.
- Параллельный mecha-стек рядом с Mech.

## Лицензия

Оригинальный код и документация этого слоя — **MIT**; оригинальные ассеты — **CC-BY-SA-3.0**.  
Подробности: [`LICENSE.md`](LICENSE.md), корневые [`LICENSE-FISH.TXT`](../../../LICENSE-FISH.TXT).
