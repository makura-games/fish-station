# Паритет реакций экстрактов и зелий SS220

Честные дыры: эффект не портирован или упрощён.

## Реакции экстракта

| Цвет | Реагент | SS220 | Статус |
| --- | --- | --- | --- |
| gold | plasma | 5 crit, кап 15 | пул враждебных (lavaland/asteroid/salvage roster) + crit-кап + release |
| gold | blood / water | 3 нейтр. / 1 друж. | `FishXenoGoldCritNeutralCarpFish` ×3, `FishXenoGoldCritFriendlyCarpFish` ×1 + кап |
| black | blood | virus_genes | реагент `FishVirusGenesFish` (не полный virology SS220) |
| cerulean | blood | territory blueprints | случайный `Blueprint*` из пула, не territory kit |
| pyrite | plasma | random paint | пул красок |
| adamantine | plasma | лист | `TreasureCoinAdamantine` |
| dark blue freeze / orange fire / oil | plasma | отложенные | упрощённые эффекты |

## Зелья

| Зелье | SS220 | Статус |
| --- | --- | --- |
| Sentience | опрос 10 с, разогрев, взрыв, остывание | raffle `FishXenoSentienceRaffleFish`; разогрев/взрыв; **−1 стадия / 60 с** |
| Transference | подтверждение | `TransferTo` |
| Fireproof | 3 заряда | Heat-урон снимает заряд |
| Oil slick | MOD | отказ на **активном** `ModsuitComponent`; предметы — Slippery |

## Машины

| SS220 | Статус |
| --- | --- |
| Консоль | BUI + **verbs** (collect/scan/feed/recycle/release) в радиусе |
| Мультитул → ресайклер | линк консоль↔ресайклер, grind с linked recycler |
| Cargo ящик | `FishXenobiologyLab` в `cargo_science.yml` |
| Ящик на карте | **нет** (SunriseMapPool не трогаем) |

## Органы

| SS220 | Статус |
| --- | --- |
| Конвейер | диссектор → масса → слайм → анализатор |
| **regen_mesh** | `FishXenoRegenMeshFish` → качество **Pristine** (2× в анализаторе) |
| Таблица диссекции | расширена под DM-список (flock/megacarp/… частично по id прототипа) |
| Эффекты | UseInHand 60 с; **HeatResist** (−50% Heat); импланты порция 3–4 (6 типов) |
| Анализатор | лимит 3 скана на тип; **tech disk** ~12% → `TechnologyDisk` |
| Уникальные органы SS220 | ~1600 строк DM не портированы (дыхание дракона, flock spell, …) |
