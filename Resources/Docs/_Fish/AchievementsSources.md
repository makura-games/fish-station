# Аудит источников достижений SS13

Числа относятся к реально найденным определениям, не к числу уже перенесённых достижений. До дедупликации одинаковые достижения разных форков учитываются отдельно.

Обзор статуса переноса: `AchievementsCatalog.md`.

## Текущие версии

### TGStation

- Repository: `tgstation/tgstation`
- Revision (файлы awards): master на момент аудита 2026-08-21
- Binary + score leaf awards с `name =`: **115**
- Файлы: `boss_achievements.dm`, `boss_scores.dm`, `job_achievements.dm`, `job_scores.dm`, `mafia_achievements.dm`, `misc_achievements.dm`, `misc_scores.dm`, `progress_scores.dm`, `skill_achievements.dm`
- Исторический аудит `misc_achievements.dm` начат (последние commits включают новые awards вроде death desensitization / meteor punch / space dragon fishing).

### BeeStation

- Repository: `BeeStation/BeeStation-Hornet`
- Leaf awards: **43**
- Значительная часть пересекается с TG; BeeCoin rewards не переносятся.

### Monkestation

- Repository: `Monkestation/Monkestation2.0`
- Leaf awards: **102**
- Каталог близок к расширенному TG; уникальные записи выделяются на этапе смысловой дедупликации.

### Shiptest

- Repository: `shiptest-ss13/Shiptest`
- Leaf awards: **47**
- Урезанный TG-набор + ship-specific entries.

### Yogstation

- Repository: `yogstation13/Yogstation`
- Файл: `code/datums/achievements/achievements.dm`
- Актуальных `/datum/achievement`: **51**
- Есть скрытые достижения; viewer скрывает условие до получения.

### Goonstation

- Repository: `goonstation/goonstation`
- Публичный каталог: [wiki Medals](https://wiki.ss13.co/Medals) revision `72754` (2026-08-09)
- Несекретных: **94**, секретных: **58**, всего: **152**
- Medal rewards / role weighting / cosmetic unlocks **не переносятся** — только условие и факт unlock.

### CM-SS13

- Repository: `cmss13-devs/cmss13`
- Subsystem: `code/controllers/subsystem/achievements.dm` (external HTTP API)
- Отдельного монолитного definition-файла нет; требуется продолжение поиска ` /datum/achievement` по модулям.
- В SS14 внешний API не используется.

## Промежуточный итог

| Метрика | Значение |
| --- | --- |
| Raw definitions | **510** |
| Unique (name-dedup) | **344** |
| Migrated | 0 |

Ещё не включено в unique:

- полная историческая выборка удалённых TG/Bee/Goon;
- CM definitions после точечного поиска;
- смысловая дедупликация и Fish-адаптации.

## Источники без account-wide каталога

`ParadiseSS13/Paradise` — в публичной версии формальной achievement system не найдено; job objectives round-only и в каталог не входят.
