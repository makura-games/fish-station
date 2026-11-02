# Паритет реакций экстрактов и зелий SS220

Честные дыры: эффект не портирован или упрощён.

## Реакции экстракта

| Цвет | Реагент | SS220 / Paradise | Статус |
| --- | --- | --- | --- |
| gold | plasma | 5 HOSTILE, задержка 5 с, кап 45 | Paradise-тайминг; пул **`FishSlimeExtractGoldCoreSpawnable`** + fallback legacy |
| gold | blood | 3 lesser (neutral) | то же (tier Lesser) |
| gold | water | 1 friendly | то же (tier Friendly) |
| black | blood | virus_genes | бутылка `FishChemistryBottleVirusGenesFish`; 35% — vial со случайным `DiseaseBlood*` (не полный SS220 virology) |
| cerulean | blood | territory blueprints | расширенный пул blueprint + **`FishCeruleanTerritoryMarkerFish`**; **1 реакция / чанк 21×21**, не полный area kit |
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
| Таблица диссекции | расширена (flock→EMP, dragon→adrenaline, herald→cryo, revenant→essence, …) |
| Эффекты | 9 базовых + **8 имплантов** + **5 spell-like** (EMP / adrenaline / slime skin / cryo burst / essence) |
| Анализатор | лимит 3/тип; tech disk ~12% |
| Уникальные органы SS220 | dragon breath, flock spell, … **не 1:1** (~1600 строк DM) |

## Сборка

`dotnet` / YAMLLinter / клиент в cloud VM **не запускались** (нет SDK в среде агента).
