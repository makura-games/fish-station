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
| black | blood | virus_genes | `Toxin` — реагента virus_genes нет в билде |
| adamantine | plasma | лист адамантина | `TreasureCoinAdamantine`, не лист |
| bluespace | blood | 25 плиток | `FishXenobiologyBluespaceTileFish` |
| bluespace | plasma | bluespace crystal | `Artifexium` в растворе |
| cerulean | blood | territory blueprints | `TechnologyDisk` — не territory |
| sepia | plasma | timestop | паралич 14 с, радиус 2 |
| sepia | water / blood | camera / 25 sepia tiles | есть |
| pyrite | plasma | random paint | пул SprayPainter / ammo / crayon |

## Зелья

| Зелье | SS220 | Статус |
| --- | --- | --- |
| Sentience | опрос призраков 10 с, разогрев, взрыв | GhostRole без опроса/разогрева |
| Transference | подтверждение игрока | `TransferTo`, старое тело → Dead |
| Fireproof | 3 заряда, снятие при огне | 3 заряда, снятие по Heat-урону на одежде |
| Oil slick | MOD нельзя | MOD в билде не найден — проверка не добавлена |

## Машины

| SS220 | Статус |
| --- | --- |
| Консоль | `FishXenobiologyConsoleFish` BUI, радиус 7, без камеры/hotkey |
| Процессор | `FishSlimeProcessorFish` |
| Ресайклер | `FishMonkeyRecyclerFish` |
| Ручной сканер | `FishXenobiologySlimeScannerFish` |
| Ящик лаборатории | `FishXenobiologyLabCrateFish` (cargo fill, карты не трогаем) |

## Органы

| SS220 | Статус |
| --- | --- |
| Диссектор | `FishXenoDissectorFish`, упрощённая таблица mob→organ |
| Слайм 50 ticks | 100 с таймер, trained adult без mind игрока |
| Анализатор | 15 с, очки R&D, без лимита 3 типа / tech disk |
| Эффекты органов | предметы без имплант-эффектов (~1600 строк SS220 не портированы) |
