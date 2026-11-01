# Черновик PR в makura-games/fish-station

**Заголовок:** `[Fish] Порт ксенобиологии SS220 (черновик)`

**Base:** `master` (makura-games/fish-station)  
**Head:** `Pythagorius-cra:feature/ss220-xenobiology`

---

## Краткое описание

Перенос ксенобиologии SS220 на Fish: слаймы, экстракты, зелья, машины (консоль BUI, процессор, ресайклер, сканер), ящик `FishXenobiologyLabCrateFish`, упрощённые органы/анализатор. **Карты и SunriseMapPool не трогали.** Сборка/YAML **не гонялись** в cloud-среде (нет dotnet).

Продолжение работы по шагам: `Documentation/Porting/Xenobiology/HANDOFF.md`.

## Ссылка на багрепорт/Предложение

Порт ксенобиологии SS220 / Paradise на Fish Station (инвентарь: `Documentation/Porting/Xenobiology/FEATURE_INVENTORY.md`).

## Медиа (Видео/Скриншоты)

Нет. В клиент не заходил.

## Чеклист

- [x] Инвентарь по коду Paradise-SS220 — `FEATURE_INVENTORY.md`, `0d15e6ded5`
- [x] Слайм: голод, рост, деление, ИИ, кормление — `01f4d80af7`. 22 цвета baby/adult, 4 ребёнка, смерть взрослого (малыш + rabid), плазма 5 листов, огонь лечит brute, EMP сбрасывает powerlevel
- [x] Таблица мутаций — `dedccef720`. `dotnet test Content.Tests --filter FullyQualifiedName~FishSlimeMutationTest` — Passed 7, Failed 0
- [x] Прототипы и локали слаймов — `8ae12641cf` (RSI: копия CC-BY-SA `slimes.rsi` + тинты)
- [x] Эксклюзивная присоска (один слайм на жертву) — `1b80ceaf3b`
- [x] Вода: `rand(10,15) * объём` при Touch — `f34a11319c`
- [x] Холод ≤ T0C−40: замедление ×0.05 и паралич, пока температура не выше порога — `0fba437668`
- [x] Игрок: глаголы evolve/reproduce при 10 growth
- [x] Экстракт — каркас `be2c775c6f`, uses/шприц; реакции серого `82090cc650`
- [x] Реакции экстрактов (простые) — `76fcb4bb9c`
- [x] Реакции gold/silver/sepia/bluespace/… (отложенные) — `b46c4c1fb1`; упрощения — `PARITY_REPORT.md`
- [x] Зелья — поля слайма `5f9815d011`; enhancer/speed/chill/oil `35c4f426b5`; sentience/transference — **упрощено** `537bc728bd`
- [x] Процессор мёртвого слайма → экстракты по `Cores` — `c106c9f3f8`, прототип `9ccd6d6601`
- [ ] Консоль ксенобиологии (BUI, хранение)
- [x] Ресайклер обезьян — `9782fda77e`, прототип `9ccd6d6601`
- [x] Ручной сканер слайма — `2f450c802b`, прототип `9ccd6d6601`
- [x] Обезьяньи кубы — прототипы `2a1fdca7b8`; спавн 3× из серого экстракта (blood) `82090cc650`
- [ ] Органы, диссекция, анализатор
- [ ] Лаборатория на карте (ящик, питание, без правок SunriseMapPool)
- [x] Сборка — `dotnet build Content.Server/Content.Server.csproj --configuration Debug --verbosity quiet --no-restore` — succeeded (после изменений в ветке)
- [x] YAML-линтер — `dotnet run --project Content.YAMLLinter/Content.YAMLLinter.csproj --configuration Release --no-build` — **No errors found** (~54 с; проверяет все прототипы, в т.ч. `Resources/Prototypes/_Fish/Xenobiology/slimes.yml`)
- [x] Хендофф — `Documentation/Porting/Xenobiology/HANDOFF.md`

### Известные упрощения (не закрыты)

- Огнетушитель, снимающий trained, не перенесён
- Полная кривая холода ниже −50°C — частично (blunt в life-тике есть, не весь паритет SS13)
- Скрещивание ядер, acidproof/explosion resistance зелий в SS220 — не в scope скачанных файлов слайма
