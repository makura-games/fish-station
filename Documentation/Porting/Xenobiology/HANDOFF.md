# Хендофф порта ксенобиологии SS220

Продолжать отсюда. Исходники Paradise заново не качать и вики не перечитывать: цифры ниже сняты с `ss220club/Paradise-SS220` `master` на дереве `0fffca1f1f29a63aff49d9b56524813eedf82c6d` (репозиторий архивный). Полный инвентарь рядом: `FEATURE_INVENTORY.md`. Этот файл — что уже в ветке и какой шаг следующий.

Ветка: `feature/ss220-xenobiology` на `Pythagorius-cra/fish-station-mane`.
Единственный PR: https://github.com/Pythagorius-cra/fish-station-mane/pull/6 (draft, base `master` форка). В `makura-games/fish-station` PR не открывать, пока об этом не попросят.
Tip на момент хендоффа: `dfe76744fc`. Upstream-база нарезки: `makura-games/fish-station` `master` `e98fb511c9`.

Не трогать чужие ветки: `feature/restore-original-music-player`, `feature/ss13-mining-system`, `feature/ss1984-crit-behavior`, `feature/clockwork-cult`, `fish/game-improvements-package`.

После каждого маленького шага: `git pull --rebase origin feature/ss220-xenobiology`, коммит, `git push origin feature/ss220-xenobiology`, и только свои пункты чеклиста в PR #6. Force-push нельзя. Один писатель. Пока слайм и экстракт не замкнуты в цикл, консоль, ресайклер, органы и лабораторию не начинать.

## Что уже в ветке

| SHA | Что это | Проверено |
| --- | --- | --- |
| `0d15e6ded5` | `FEATURE_INVENTORY.md` | документ, не геймплей |
| `dedccef720` | таблица мутаций, `FishSlimeComponent`, `FishSlimeMutationTest` | `dotnet test Content.Tests --filter FullyQualifiedName~FishSlimeMutationTest` — Passed 7, Failed 0 |
| `8ae12641cf` | 22 цвета, baby/adult прототипы, локали, RSI | сборка сервера ниже |
| `2a1fdca7b8` | прототипы кубов `FishXenobiologyMonkeyCube`, wrapped, box. Гидратация — ванильный `Rehydratable` / `MonkeyCube` | в игре не смотрели. Спавн из серого экстракта (blood) — `82090cc650` |
| `01f4d80af7` | голод, рост, деление на 4, охота, присасывание, смерть взрослого | `dotnet build Content.Server/Content.Server.csproj --configuration Debug --verbosity quiet --no-restore` — succeeded |
| `5c35445a5e` | `PROGRESS.md` (устарел относительно коммитов ниже) | — |
| `be2c775c6f` | каркас экстракта: 100 единиц, uses 1..5, память шприца, локали, прототип, спрайт | YAML-линтер ранее без ошибок. В клиент не заходили |
| `82090cc650` | реакции **серого** экстракта: plasma → малыш, water 5 → epinephrine 3, blood → 3 куба; `ConsumeUse` | сборку/dotnet в этой сессии не гоняли |
| `5f9815d011` | зелья stabilizer / mutator / steroid / docility: компонент, система, `potions.yml`, локали | в игре не смотрели |
| `76fcb4bb9c` | реакции экстрактов: green…rainbow (простые reagent/spawn/зелья), red bloodlust; дыры — `PARITY_REPORT.md` | сборку/YAML не гоняли |
| `35c4f426b5` | зелья enhancer / speed / fireproof / oil slick + предметы sentience & transference (заглушка); chill coating | в игре не смотрели |
| `b46c4c1fb1` | отложенные и «сложные» реакции экстрактов (gold…pyrite, oil/orange/darkblue delay) — `FishSlimeExtractDeferredReactionSystem` | сборку/YAML не гоняли |
| `537bc728bd` | sentience / transference — GhostRole + `TransferTo` (без опроса 10 с SS220) | в игре не смотрели |
| `c106c9f3f8` | процессор мёртвого слайма → `Cores` экстрактов; `machines.yml` | в игре не смотрели |
| `9782fda77e` | ресайклер 5 мёртвых обезьян → 1 `FishXenobiologyMonkeyCube` | в игре не смотрели |
| `2f450c802b` | ручной сканер (`FishXenobiologySlimeScannerFish`) | в игре не смотрели |
| `5926cea88a` | консоль ксенобиологии BUI + радиус 7 (`FishXenobiologyConsoleFish`) | в игре не смотрели |
| `f53815f8e7` | ящик `FishXenobiologyLabCrateFish` | на карту не ставили |
| `43719fae99` | диссектор / масса / слайм / анализатор (каркас) | в игре не смотрели |
| `870e8cfd3e` | chill заряды при Heat; pyrite/cerulean паритет | сборку не гоняли |

Сборка и тест — только то, что в таблице. YAML-линтер после расширения `potions.yml` не запускался. В игре цикл не гонялся.

Уже известные упрощения в коде слайма, не равные SS13: игрок на слайме сам не делится без глагола (глаголы есть). Огнетушитель trained не перенесён. Полная кривая холода ниже −50°C — частично.

## Куда класть код

Уже есть:

- `Content.Shared/_Fish/Xenobiology/Slimes/FishSlimeComponent.cs`
- `Content.Shared/_Fish/Xenobiology/Slimes/FishSlimeMutation.cs`
- `Content.Server/_Fish/Xenobiology/Slimes/FishSlimeSystem.cs`
- `Content.Shared/_Fish/Xenobiology/Extracts/FishSlimeExtractComponent.cs`
- `Content.Shared/_Fish/Xenobiology/Extracts/FishSlimeExtractSystem.cs` + `FishSlimeExtractSystem.Reactions.cs` + `FishSlimeExtractReactions.Colors.cs` — реакции по color; gold/silver/… — `FishSlimeExtractDeferredReactionSystem` (Server).
- `Content.Server/_Fish/Xenobiology/Extracts/FishSlimeExtractDeferredReactionSystem.cs`, `FishSlimeExtractGoldMobCap.cs`, `FishSlimeExtractPendingReactionComponent.cs`
- `Content.Shared/_Fish/Xenobiology/Extracts/FishSlimePotionComponent.cs`, `FishSlimeChillCoatingComponent.cs`
- `Content.Server/_Fish/Xenobiology/Extracts/FishSlimePotionSystem.cs` (+ `Extract` / `Gear` / `Sentience` / `SentienceSlime`) — зелья на слайм, экстракт, одежду, предмет, животное.
- `Content.Tests/_Fish/Xenobiology/FishSlimeMutationTest.cs`
- `Resources/Prototypes/_Fish/Xenobiology/slimes.yml`
- `Resources/Prototypes/_Fish/Xenobiology/extracts.yml`
- `Resources/Prototypes/_Fish/Xenobiology/potions.yml`
- `Resources/Prototypes/_Fish/Xenobiology/machines.yml`, `tiles.yml`
- `Resources/Prototypes/_Fish/Xenobiology/cubes.yml`
- `Resources/Locale/en-US/_Fish/xenobiology/` и `Resources/Locale/ru-RU/_Fish/xenobiology/` (`slimes.ftl`, `extracts.ftl`, `cubes.ftl`, `potions.ftl`)
- `Documentation/Porting/Xenobiology/PARITY_REPORT.md` — нереализованные реакции и упрощения зелий.
- `Resources/Textures/_Fish/Xenobiology/extracts/extracts.rsi/`

Дальше, не смешивая с чужими папками:

- Реакции и зелья: `Content.Server/_Fish/Xenobiology/Extracts/` и прототипы `reactions.yml` / `potions.yml` рядом с уже существующими.
- Машины (консоль ещё нет): `Content.Shared/_Fish/Xenobiology/Machines/`, `Content.Server/_Fish/Xenobiology/Machines/`, `machines.yml`.
- Органы (позже): `Content.Shared/_Fish/Xenobiology/Organs/`, `Content.Server/_Fish/Xenobiology/Organs/`.
- Кубы уже в `Cubes` не выделены: прототипы в `cubes.yml`, система не нужна — серый экстракт (blood) спавнит `FishXenobiologyMonkeyCube`.

Маркеры только `// Fish-edit` и `// Fish-start` / `// Fish-end`, пояснение по-русски. Новые прототипы: id с префикса `Fish` и `suffix: Fish`. `[Dependency]` не `readonly`. Системы `partial`. Строки игрока только через локали.

## Что переиспользовать

- Урон: `DamageableSystem`. Голод слайма — поле `FishSlimeComponent.Nutrition`, не кухонный голод.
- Таймер жизни 2 секунды (`NextLife` / `NextAi`), не каждый кадр.
- Шприц и раствор экстракта: штатные `InjectableSolution` / `Solution`. Объём раствора 100. `LastInjector` уже есть под лог взрыва oil.
- Обезьяна: живой `MobMonkey` (`MonkeyAccentComponent`). Куб: родитель `MonkeyCube`, вода через `Rehydratable`.
- Консоль без камерного глаза (его в билде нет): команды по слаймам и обезьянам в радиусе лаборатории, доступ Research, питание. Дыру писать в паритет, камеру станции не изображать.
- Ресайклер: мёртвый `MobMonkey`, 5 туш → 1 `MonkeyCube`. Типы кубов, которых нет в билде, не подменять. Рейтинг деталей не крутить.
- Процессор: отдельная машина, только мёртвый слайм, выход = `Cores` экстрактов своего цвета. Рейтинг stock parts не портировать, пока у машины нет деталей.
- Пол сепии и блюспейса (по 25 плиток): не новый мувер. Кладка через существующий `ContentTileDefinition`, замедление — поле friction тайла. `SlipperyComponent` только если friction тайла это не выражает. В коде плиток ещё нет. Смотрели `Content.Shared/Maps/ContentTileDefinition.cs`, `Resources/Prototypes/Tiles/basic.yml` и `SlipperyComponent`.
- Лаборатория: ящик науки (консоль, процессор, ресайклер, сканер, анализатор, диссектор, серый малыш, кубы, плазма, шприц, кровь), питание от APC, доступ Research. В `SunriseMapPool` карту не добавлять и существующие гриды не переписывать.
- Спрайты слайма: уже скопирован CC-BY-SA `slimes.rsi` плюс цветовой множитель. `icons/mob/slimes.dmi` SS220 целиком не тащить.
- Органы без честного эффекта на урон, скорость или реагенты не помечать сделанными. Анализатор — очки ближайшему research server.

Скрещивание ядер не делать: `SLIME_EXTRACT_CROSSING_REQUIRED 10` есть, логики в скачанных файлах слайма нет. Зелий acidproof и explosion resistance в этих файлах нет. Взрыв — реакция oil, огнезащита — chill potion.

## Слайм: цифры

Оригинал: `code/modules/mob/living/simple_animal/slime/slime_mob.dm`, `slime_life.dm`, `slime_powers.dm`, `slime_death.dm`, `subtypes.dm`. Порог: `#define SLIME_EVOLUTION_THRESHOLD 10` в `code/__DEFINES/mob_defines.dm`.

- Малыш: HP 150, nutrition старт 700, max 1000, grow 800, hunger 500, starve 200.
- Взрослый: HP 200, max 1200, grow 1000, hunger 600, starve 300.
- `mutation_chance` старт 30. Ребёнок: `clamp(parent ± 5, 0, 100)`.
- `cores` старт 1, потолок 5. `powerlevel` 0–10.
- Docile держит nutrition 700 и не охотится.
- Каждые life: 15% сжечь `1 + adult` nutrition. На нуле 75% шанс урона. Выше порога роста: −20 nutrition и +1 growth, пока не 10.
- На 10 growth без цели, без кормления и без игрока: малыш эволюционирует, взрослый делится на 4 малышей.
- Если `mutation_chance >= 100` — rainbow. Иначе `prob(mutation_chance)` берёт один из четырёх слотов, иначе цвет родителя. Слот — `rand(1,4)`, повтор цвета в таблице утяжеляет его.
- Nutrition ребёнка игрока = 90% родителя. Powerlevel ребёнка = powerlevel/4. Игрок переносится в случайного ребёнка, родитель удаляется.
- Смерть взрослого (не gib): бешеный малыш того же цвета, сам откатывается в малыша и оживает. Малыш умирает. Gib удаляет.
- Огонь лечит brute (снаряды burn тоже). Вода: `rand(10,15) * volume` brute. Холод ниже 5°C копит урон, ниже −40°C стан. Жара ≥ 57°C ускоряет.
- Морфин ×2 к задержке, frostoil ×5. EMP обнуляет powerlevel.
- Кормление: живой не-слайм не-силикон. Carbon: clone 2–4 и tox 1–2 за тик, слайм +7–15 nutrition и −3 brute. Один слайм на жертву. Смерть жертвы-игрока-человека: 85% rabid.
- Плазма: 5 листов. Если уже есть Discipline и не trained — trained. Иначе позитивная дисциплина.
- Цель: радиус 7.

Поля компонента, которые зелья уже могут менять: `MutationChance`, `MutatorUsed`, `Cores`, `Rabid`, `Docile`, `Trained`, `Discipline`, `Nutrition`, `PowerLevel`.

## Таблица мутаций

`mutation_table()` в `subtypes.dm`.

| Цвет | Слоты 1–4 |
| --- | --- |
| grey | orange, metal, blue, purple |
| purple | dark purple, dark blue, green, green |
| metal | silver, yellow, gold, gold |
| orange | dark purple, yellow, red, red |
| blue | dark blue, silver, pink, pink |
| dark blue | purple, blue, cerulean, cerulean |
| dark purple | purple, orange, sepia, sepia |
| yellow | metal, orange, bluespace, bluespace |
| silver | metal, blue, pyrite, pyrite |
| pink | pink, pink, light pink, light pink |
| red | red, red, oil, oil |
| gold | gold, gold, adamantine, adamantine |
| green | green, green, black, black |
| light pink, oil, black, adamantine, bluespace, cerulean, sepia, pyrite, rainbow | четыре раза свой цвет |

`random_colour()` / rainbow-плазма — равномерно из 22 имён, включая rainbow.

## Экстракт и реакции

Оригинал предмета: `/obj/item/slime_extract` в `xenobiology.dm`. 1 использование, реагенты 100, enhancer +1 до 5. `associated_gun_type` на предмете есть, реакции его не спавнят.

Процессор: `code/modules/food_and_drinks/kitchen_machinery/processor.dm`, рецепт `food_processor_process/mob/slime`. Живой слайм вылезает. Мёртвый даёт `cores + rating - 1` экстрактов своего `coretype`. В порте рейтинг не переносим: выход = `Cores`.

Реакции: `code/modules/reagents/chemistry/recipes/slime_extracts.dm`. Контейнер — экстракт. Реагент тратится, использование снимается. Объёмы как в DM.

| Реакция | Цвет | Реагент | Продукт |
| --- | --- | --- | --- |
| Slime Spawn | grey | plasma 1 | серый малыш |
| Epinephrine | grey | water 5 | epinephrine 3 |
| Monkey | grey | blood 1 | 3 monkey cube |
| Mutation toxin | green | plasma 1 | mutationtoxin 1 |
| Metal | metal | plasma 1 | plasteel 5 + metal 15 |
| Glass | metal | water 1 | rglass 5 + glass 15 |
| Smart metal | metal | blood 1 | synthetic omnizine 1 |
| Crit | gold | plasma 1 | 5 враждебных мобов, задержка, общий кап |
| Crit lesser | gold | blood 1 | 3 нейтральных |
| Crit friendly | gold | water 1 | 1 дружелюбный |
| Bork | silver | plasma 1 | 4–6 случайной еды |
| Bork 2 | silver | water 1 | 4–6 случайных напитков |
| Frost oil | blue | plasma 1 | frostoil 10 |
| Stabilizer potion | blue | blood 1 | зелье стабилизатора |
| Freeze | dark blue | plasma 1 | через ~5 с −240 температуры, радиус 7 |
| Fireproof potion | dark blue | water 1 | chill potion, 3 заряда |
| Capsaicin | orange | blood 1 | capsaicin 10 |
| Fire | orange | plasma 1 | через ~5 с горячий плазменный выброс |
| EMP | yellow | blood 1 | EMP 3/7 |
| Powercell | yellow | plasma 1 | high slime cell |
| Glow | yellow | water 1 | slime flashlight |
| Steroid | purple | plasma 1 | стероид |
| Jam | purple | sugar 1 | slimejelly 10 |
| Plasma sheets | dark purple | plasma 1 | plasma sheet 3 |
| Mutator | red | plasma 1 | мутатор |
| Bloodlust | red | blood 1 | видимые слаймы: снять docile, иначе rabid |
| Speed | red | water 1 | speed treat |
| Docility | pink | plasma 1 | docility potion |
| Omnizine | pink | blood 1 | omnizine 1 |
| Advanced mutation | black | plasma 1 | amutationtoxin 1 |
| Virus genes | black | blood 1 | virus_genes 1 |
| Explosion | oil | plasma 1 | через 5 с взрыв devastation 1 / heavy 3 / light 6, лог `LastInjector` |
| Oil potion | oil | blood 1 | oil slick potion |
| Sentience | light pink | plasma 1 | sentience potion |
| Adamantine | adamantine | plasma 1 | лист адамантина |
| Bluespace floor | bluespace | blood 1 | 25 блюспейс-плиток |
| Crystal | bluespace | plasma 1 | bluespace crystal |
| Enhancer | cerulean | plasma 1 | extract enhancer, зовёт `TryAddUse` |
| Territory | cerulean | blood 1 | cerulean blueprints, один раз на зону |
| Timestop | sepia | plasma 1 | хронополе 140 тиков, радиус 2, автор иммунен |
| Camera | sepia | water 1 | камера + плёнка |
| Sepia floor | sepia | blood 1 | 25 сепия-плиток |
| Paint | pyrite | plasma 1 | случайная банка краски |
| Random core | rainbow | plasma 1 | случайный слайм, 50% взрослый |
| Transfer | rainbow | blood 1 | consciousness transference potion |

У gold, silver и purple третьей реакции в этом файле нет. `MAX_GOLD_CORE_MOBS` в скачанных файлах не попался: кап зафиксировать в коде и написать в паритет.

## Зелья

`xenobiology.dm`, от `/obj/item/slimepotion`.

- Stabilizer: `MutationChance` −15, не ниже 0. Мёртвый и не-слайм — отказ.
- Mutator: +12, один раз (`MutatorUsed`), не если уже 100.
- Steroid: только малыш, `Cores` +1, потолок 5.
- Enhancer: на экстракт, uses +1, потолок 5. Метод `TryAddUse` уже есть.
- Docility: снять rabid без полной docile; иначе docile, nutrition 700, имя от игрока.
- Speed: bodytemperature 550, джиттер 15 с, не стакается если уже горячий. Слайм может съесть сам.
- Fireproof: одежда, 3 заряда, FIRE_PROOF и максимальная теплозащита.
- Oil slick: slowdown предмета в 0, либо стол `SlipperyComponent`. Активный MOD нельзя.
- Sentience: опрос призраков 10 с, причина обязательна. Уже с разумом, мёртвые и неорганика — отказ. Два холостых греют зелье, третье взрывается. Остывание −1 стадия за 60 с.
- Transference: игрок подтверждает, разум уходит в животное, старое тело умирает. Роли сохраняются.

## Машины

Консоль: `FishXenobiologyConsoleFish` — BUI, до 6 слаймов, счётчик кубов, зелье через клик, радиус 7. **Нет:** камерный глаз, shift/ctrl hotkey, линк мультитулом ресайклера, area xenobiology_compatible.

Ресайклер / процессор / сканер: `machines.yml`. Ящик: `lab_crate.yml`. Органы: `organs.yml` + `Content.* / Organs/` — упрощённый конвейер, без таблицы SS220 и без gameplay-эффектов органов.

## Следующие шаги, по порядку

1. Не качать SS220. Открыть этот файл и `FEATURE_INVENTORY.md`.
2. ~~Серый экстракт~~ — `82090cc650`.
3. ~~Базовые зелья полей слайма~~ — `5f9815d011`. ~~Enhancer / speed / fireproof / oil slick~~ — `35c4f426b5`; sentience & transference — предмет есть, логика нет (`PARITY_REPORT.md`).
4. ~~Простые реакции экстрактов по цветам~~ — `76fcb4bb9c`. ~~Отложенные gold/silver/sepia/…~~ — `b46c4c1fb1` (упрощения — `PARITY_REPORT.md`).
5. ~~Процессор мёртвого слайма~~ — `c106c9f3f8` + `machines.yml` `9ccd6d6601`.
6. ~~Консоль, ресайклер, сканер~~ — консоль `5926cea88a`, остальное ранее. ~~Ящик лаборатории~~ — `f53815f8e7`. ~~Органы (каркас)~~ — `43719fae99`.
7. **Дальше:** cargo-заказ ящика (если нужен), `dotnet build` + YAMLLinter, имплант-эффекты органов, sentience poll, gold mob release, карта (только по отдельному запросу).

## Уточнения из дампа слайма

Дамп писался на `5c35445a5e`, до каркаса экстракта `be2c775c6f`. Это не новый разбор SS220.

- Id цветов в коде без пробела: `darkblue`, `darkpurple`, `lightpink`. Прототип: `FishSlime{Pascal}BabyFish` / `AdultFish` (Grey, DarkBlue, LightPink, …). Неизвестный цвет падает в Grey.
- Life 2 с, шаг ИИ 0.5 с. Рост: 15% за тик сжечь `1 + adult` nutrition. На нуле 75% шанс 0–5 brute. Пассивное лечение: 30% и −1 blunt.
- Присасывание эксклюзивно: второй слайм на ту же жертву не цепляется (`IsVictimAlreadyLatched`). Охота в радиусе 7: просто голодный берёт только `MonkeyAccentComponent`; голодающий, rabid или `attacked > 0` — любой живой не-слайм. Силиконов отдельно нет.
- `attacked` потолок 50. Рука: 4% дисциплина. Удар: `attacked += 10`, 25% дисциплина; если присосан, 40% отцепиться. Trained: 20% шанс сбить trained ударом. Дисциплина тает 10% за тик, если не trained. Rabid снимается на 60%, если discipline ≥ 5.
- Плазма: `SharedStackSystem.TryUse(uid, 5)`. `StackComponent.Count` не читать, он закрыт.
- Смерть взрослого в порте: два спавна (бешеный малыш и обычный с разумом), труп удаляется. В SS220 тот же моб откатывается в малыша. Так сделано, потому что `MobThresholdsComponent` закрыт для записи снаружи `MobThresholdSystem`.
- Вода: `rand(10,15) * volume` в `OnWaterReaction`, попап 25%. Огнетушитель, который снимает trained, не перенесён.
- Жара ≥ 330.23: скорость ×1.6. Ниже 233.15 (T0C−40): скорость ×0.05 и паралич, пока температура ≤ порога (`RefreshFrozenParalyze`, снятие stun при отогреве). Ниже 223.15: дополнительный blunt 1–20 в life-тике.
- Морфин ×2 и FrostOil ×5 через `GetTotalPrototypeQuantity`. `BloodstreamComponent` не трогать.
- Сила: при nutrition роста и powerlevel < 10 шанс `0.30 - powerlevel * 0.02`. Выше hunger+100 и powerlevel < 5: 25%.
- Речь: 2% за тик, короткий набор фраз. Оверлеи настроения не подключены. RSI state `aslime-_3` в копии есть.
- Спрайты: blue/green/yellow/rainbow без тинта. Остальные — `blue_adult_slime` с hex. Rainbow baby/adult оба на `rainbow_baby_slime`. Малыш scale 0.72, взрослый 1. Копия `Resources/Textures/Mobs/Aliens/slimes.rsi`, CC-BY-SA-3.0.
- Родители прототипа: `SimpleMobBase`, `MobCombat`, `NpcFactionMember` / `SimpleNeutral`, речь `Slime`, `FootstepSlime`, модификатор урона `Slime`, `VentCrawler`.
- `RobustToolbox` в том чекауте не было. Его инициализировали локально на `36905986f6809420dbc78168fc494f91723d356b` и не коммитили. `dotnet` брали из `~/.dotnet` 10.0.403.

Реакции, машины, консоль, ящик, органы-каркас — до `870e8cfd3e`. Сборку/YAML/клиент **не запускали** в cloud VM.
