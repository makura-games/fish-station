# Паритет реакций экстрактов и зелий SS220

Честные дыры: эффект не портирован или упрощён.

## Реакции экстракта

| Цвет | Реагент | SS220 / Paradise | Статус |
| --- | --- | --- | --- |
| gold | plasma | 5 HOSTILE, задержка 5 с, кап 45 | **Paradise:** `MAX_GOLD_CORE_MOBS` 45, таймер 5 с, пул `FishSlimeExtractGoldCritPools.HostilePlasma` |
| gold | blood | 3 lesser (neutral) | Пул `LesserBlood` + нейтральные прототипы |
| gold | water | 1 friendly | Пул `FriendlyWater` |
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
| **regen_mesh** | `FishXenoRegenMeshFish` → **Pristine** |
| Таблица диссекции | расширена (flock→EMP, dragon→adrenaline, colossus→slime skin, …) |
| Эффекты | 9 видов UseInHand + **6 имплантов** + **3 spell-like** (EMP / adrenaline / slime skin) |
| Анализатор | лимит 3/тип; tech disk ~12% |
| Уникальные органы SS220 | flock spell, dragon breath, revenant… **не 1:1** (~1600 строк DM) |
