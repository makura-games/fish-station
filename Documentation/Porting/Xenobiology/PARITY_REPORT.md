# Паритет реакций экстрактов и зелий SS220

Честные дыры: эффект не портирован или упрощён.

## Реакции экстракта

| Цвет | Реагент | SS220 | Статус |
| --- | --- | --- | --- |
| gold | plasma | 5 враждебных мобов, кап 15 | `MobSpiderAngryBase`, кап без release при смерти |
| gold | blood / water | 3 / 1 нейтральных карпа | `MobCarp`, не полный набор SS220 crit |
| silver | plasma / water | bork 4–6 | фиксированный пул прототипов |
| dark blue | plasma | freeze −240, радиус 7 | отложено 5 с, `ChangeHeat`, не SetTemperature −240 |
| orange | plasma | fire burst | отложено 5 с, поджиг в радиусе 3 |
| yellow | blood | двойной EMP | есть |
| oil | plasma | взрыв + лог | отложено 5 с, упрощённый взрыв |
| black | blood | virus_genes | `Toxin` — реагента virus_genes нет |
| adamantine | plasma | лист адамантина | `TreasureCoinAdamantine`, не лист |
| bluespace | blood | 25 плиток | `FishXenobiologyBluespaceTileFish` (parent sepia tile) |
| bluespace | plasma | bluespace crystal | `Artifexium` в растворе |
| cerulean | blood | territory blueprints | `PaperOffice` — заглушка |
| sepia | plasma | timestop | паралич 14 с, радиус 2, без полного timestop |
| sepia | water / blood | camera / 25 sepia tiles | есть |
| pyrite | plasma | random paint | только `SprayPainter` |

## Зелья

| Зелье | SS220 | Статус |
| --- | --- | --- |
| Sentience | опрос призраков 10 с, разогрев, взрыв | GhostRole без опроса/разогрева |
| Transference | подтверждение игрока | `TransferTo`, старое тело → Dead |
| Fireproof | 3 заряда, снятие при огне | только нанесение FireProtection, заряды не тратятся |
| Oil slick | MOD нельзя | без проверки MOD |

## Машины

| SS220 | Статус |
| --- | --- |
| Консоль (хранение, кубы, зелье, хоткеи) | **нет** — нужен BUI + контейнеры |
| Процессор | `FishSlimeProcessorFish`, InteractUsing мёртвый слайм |
| Ресайклер | `FishMonkeyRecyclerFish`, 5 → 1 куб |
| Ручной сканер | `FishXenobiologySlimeScannerFish` |
| Лаборатория / ящик на карте | нет |
