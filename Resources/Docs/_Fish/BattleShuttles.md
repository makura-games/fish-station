# Боевые шаттлы Fish Station

Полная документация **Battle Shuttle** для разработчиков контента Fish Station / SS14.

Battle Shuttle — **специализированный тип меха** (`BaseMechPod`), а не отдельная подсистема транспорта и не MapGrid-шаттл.

## Документы

| Документ | Содержание |
| --- | --- |
| **Этот файл** | Обзор, компоненты, Prototype, практики |
| [`BattleShuttles-Architecture.md`](BattleShuttles-Architecture.md) | Архитектурное решение |
| [`BattleShuttles-Audit.md`](BattleShuttles-Audit.md) | Исторический аудит до lean-рефакторинга |
| [`Mechs.md`](Mechs.md) | Соседний слой Fish Mech |

## Код и прототипы

- прототипы: `Resources/Prototypes/_Fish/BattleShuttles/`
- код: `Content.Shared|Server/_Fish/BattleShuttles/`
- локали: `Resources/Locale/{en-US,ru-RU}/_*/*battleshuttles.ftl`
- тест: `Content.IntegrationTests/Tests/_Fish/BattleShuttleTest.cs`

---

# Общая архитектура

## Устройство

```
BaseMechPod (Sunrise)
 └─ BaseBattleShuttle          # BattleShuttle + Mech whitelist + Strap + fixtures
     ├─ Light / Medium / Heavy / Experimental / Civilian (абстрактные классы)
     └─ конкретные модели (Civilian, Security, Syndicate, …)
```

Пилот, батарея, BUI, actions, воздух, EMP, урон, оружие (энергия меха), appearance кабины — **Mech**.

Собственный слой отвечает только за:

- ключевой замок и блокировку посадки;
- сервисный люк (установка модулей);
- уникальные слоты / совместимость классов;
- модификаторы скорости и массы от модулей;
- пассажирский Strap;
- ore scoop.

## Компоненты

| Компонент | Назначение |
| --- | --- |
| `BattleShuttle` | Unlocked, HatchOpen, ClassTags, lock state, density/passenger base |
| `BattleShuttleModule` | Slot, OccupantMod, Mass/Walk/Sprint modifiers, CompatibleShuttleTags, Cost |
| `BattleShuttleLock` / `Key` / `LockBuster` | Ключевой замок |
| `BattleShuttleOreScoop` | Автосбор по тегу при движении |
| `Mech` + `MechEquipment` | Всё остальное |

Client-системы нет: визуал кабины = `MechVisuals.Open` (пусто/занято). Люк — логическое состояние обслуживания.

## Системы и события

`SharedBattleShuttleSystem`:

- `MechEntryEvent` / `CanDropTargetEvent` — замок;
- `InteractUsing` + crowbar — люк (только при **закрытой** `WiresPanel`);
- `MechEquipmentInserted/Removed` — refresh производного состояния;
- `RefreshMovementSpeedModifiersEvent` — модули;
- ключ / импринт замка.

`BattleShuttleSystem` (Server):

- `AfterInteract` на `MechEquipment` **before** `MechEquipmentSystem` — люк/слот/совместимость;
- `MoveEvent` + кэш `HasActiveOreScoop` — сбор руды;
- lock buster doAfter;
- MapInit refresh после startingEquipment.

## Жизненный цикл

1. Spawn / MapInit Mech → startingEquipment + батарея.
2. BattleShuttle MapInit → RefreshDerivedState.
3. Посадка через Mech; отказ при `Unlocked == false`.
4. Лом при закрытой wires panel → toggle HatchOpen.
5. Лом при открытой wires panel → снятие батареи Mech.
6. Установка модуля игроком → проверка люка → DoAfter Mech → insert → refresh.
7. Движение → ore scoop при наличии модуля.

## Принцип Prototype

Общее в абстрактных родителях. Модель меняет только спрайт, Mech states, loadout, access, classTags.

---

# Создание нового шаттла

1. Выбрать класс: `LightBattleShuttle` / `MediumBattleShuttle` / `HeavyBattleShuttle` / `ExperimentalBattleShuttle` / `CivilianBattleShuttle`.
2. Добавить entity в `shuttles.yml`.
3. Задать `Sprite` layers + `Mech` base/open/broken states.
4. При необходимости: `startingEquipment`, `AccessReader`, `RadarBlip`, `MobThresholds`.
5. Прописать `BattleShuttle.classTags`.
6. Локали `ent-*` / `ent-*-desc`.

C# менять не нужно.

```yaml
- type: entity
  id: BattleShuttleScout
  parent: LightBattleShuttle
  name: scout battle shuttle
  description: Fast recon craft.
  components:
  - type: Sprite
    layers:
    - state: pod_civ
      map: ["enum.MechVisualLayers.Base"]
  - type: Mech
    baseState: pod_civ
    openState: pod_civ_open
    brokenState: pod_civ_broken
  - type: BattleShuttle
    classTags:
    - BattleShuttle
    - BattleShuttleLight
```

---

# Создание нового модуля / оружия

## Модуль

`parent: BaseBattleShuttleModule` + `BattleShuttleModule` + нужные Content-компоненты (`Storage`, `NavMapBeacon`, `RadarBlip`, `BattleShuttleOreScoop`…).

## Оружие

`parent: BaseBattleShuttleWeapon` + `Gun` + `BatteryAmmoProvider` (питание от батареи меха через Mech gun systems).

```yaml
- type: entity
  id: BattleShuttleWeaponPulse
  parent: BaseBattleShuttleWeapon
  name: shuttle pulse mount
  components:
  - type: Sprite
    sprite: Objects/Specific/Mech/mecha_equipment.rsi
    state: mecha_laser
  - type: BattleShuttleModule
    slot: weapon
    cost: 1800
    massModifier: 1.1
    compatibleShuttleTags:
    - BattleShuttleHeavy
  - type: Gun
    fireRate: 2
    selectedMode: FullAuto
    availableModes: [FullAuto]
  - type: BatteryAmmoProvider
    proto: RedLaser
    fireCost: 30
  - type: AmmoCounter
```

Новое **поведение** (не покрытое Gun/Storage/OreScoop) → новый маленький компонент + подписка в BattleShuttleSystem. Не плодить параллельные стеки.

---

# Наследование Prototype

```
BaseMechPod
 └─ BaseBattleShuttle
     ├─ LightBattleShuttle → CivilianBattleShuttle → BattleShuttleCivilian
     │                    → BattleShuttleBlack
     ├─ MediumBattleShuttle → BattleShuttleSecurity / Industrial
     ├─ HeavyBattleShuttle → BattleShuttleSyndicate
     └─ ExperimentalBattleShuttle → BattleShuttleGold
```

Новое семейство: абстрактный класс от `BaseBattleShuttle` с `classTags`, скоростью, health, `maxEquipmentAmount`, density → конкретные модели.

---

# Параметры

## BattleShuttle

| Поле | Смысл |
| --- | --- |
| `unlocked` | Можно ли сесть |
| `hatchOpen` | Сервисный люк (установка модулей) |
| `basePassengerCapacity` | Места без модулей |
| `classTags` | Класс для совместимости |
| `requireOpenHatchForInstall` | Требовать открытый люк для игрока |
| `baseFixtureDensity` | База для MassModifier |

## BattleShuttleModule

| Поле | Смысл |
| --- | --- |
| `slot` | Уникальный id слота (`weapon`, `cargo`, `lock`…) |
| `occupantMod` | +пассажиры |
| `massModifier` / `walkSpeedModifier` / `sprintSpeedModifier` | Множители |
| `compatibleShuttleTags` | Пусто = любой |
| `cost` | Каталог (не геймплей) |

## BattleShuttleOreScoop

| Поле | Смысл |
| --- | --- |
| `range` | Радиус |
| `scoopTag` | Тег собираемых сущностей |

## Mech (критичные поля)

`maxEquipmentAmount`, `startingEquipment`, `equipmentWhitelist`, `baseState`/`openState`/`brokenState`, `airtight`.

---

# Лучшие практики

1. Сначала Mech/Prototype, потом свой C#.
2. Лом: закрытая panel = люк; открытая = батарея.
3. Не дублировать Mech BUI / entry / battery.
4. Слоты — lowercase строки.
5. Storage-модули: `UserInterface` + `ContainerContainer.storagebase`.
6. Не оставлять placeholder-сущности без поведения.
7. Hot path ore scoop только при `HasActiveOreScoop`.

---

# Примеры

- Лёгкий: `BattleShuttleCivilian`, `BattleShuttleBlack`
- Тяжёлый: `BattleShuttleSyndicate`
- Экспериментальный: `BattleShuttleGold`
- Модуль: `BattleShuttleCargoOre` (Storage + OreScoop)

---

# Производительность

- `HasActiveOreScoop` кэш — нет перебора модулей на каждый `MoveEvent` без scoop.
- Lookup в переиспользуемый `HashSet<Entity<TagComponent>>`.
- Нет LINQ в hot path.
- Нет Client system / лишней сетевой appearance для люка.
- Модульные speed modifiers через стандартный `RefreshMovementSpeedModifiersEvent`.

## Лицензия

Оригинальный код / прототипы / документация — **MIT**; оригинальные ассеты — **CC-BY-SA-3.0**.  
Временный визуал корпуса/модулей использует stock `Objects/Specific/Mech/mecha*.rsi` (CC-BY-SA) до появления собственных спрайтов Fish.  
Подробности: [`LICENSE.md`](LICENSE.md), [`LICENSE-FISH.TXT`](../../../LICENSE-FISH.TXT).

---

# История разработки

1. Первая итерация на Mech-стеке → стабилизация CI.
2. Аудит → data-driven BattleShuttle C# и иерархия Prototype.
3. Lean-рефакторинг: тонкая специализация Mech, hatch vs battery, install gate ordering, ore scoop cache.
4. Документация и журнал коммитов на ветке PR #319.
