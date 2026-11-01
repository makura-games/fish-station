# Паритет реакций экстрактов и зелий SS220

Честные дыры: эффект не портирован или упрощён.

## Реакции экстракта

| Цвет | Реагент | SS220 | Статус |
| --- | --- | --- | --- |
| gold | plasma | 5 crit, кап 15 | кап + `FishSlimeExtractGoldCritComponent` release при смерти |
| gold | blood / water | 3 / 1 carp | `MobCarp`, не полный SS220 crit |
| black | blood | virus_genes | реагент `FishVirusGenesFish` (не полный virology SS220) |
| cerulean | blood | territory blueprints | случайный `Blueprint*` из пула, не territory kit |
| pyrite | plasma | random paint | пул красок |
| adamantine | plasma | лист | `TreasureCoinAdamantine` |
| dark blue freeze / orange fire / oil | plasma | отложенные | упрощённые эффекты |

## Зелья

| Зелье | SS220 | Статус |
| --- | --- | --- |
| Sentience | опрос 10 с, разогрев, взрыв | raffle `FishXenoSentienceRaffleFish`; разогрев/взрыв на неудачных применениях |
| Transference | подтверждение | `TransferTo` |
| Fireproof | 3 заряда | Heat-урон снимает заряд |
| Oil slick | MOD | MOD-тега в билде нет |

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
| Эффекты | **порция 1:** UseInHand 60 с Regenerative/Toxic/Sinew (не импланты) |
| Анализатор | без лимита 3 типа / tech disk |
