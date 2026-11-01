# Черновик PR в makura-games/fish-station

**Заголовок:** `[Fish] Порт ксенобиологии SS220 (черновик)`

**Base:** `master` (makura-games/fish-station)  
**Head:** `Pythagorius-cra:feature/ss220-xenobiology`

---

## Краткое описание

Перенос ксенобиологии SS220 (Paradise) на Fish: слаймы (22 цвета, малыш/взрослый), голод, рост, деление по таблице мутаций, охота и присасывание, смерть взрослого, каркас экстракта и прототипы обезьяньих кубов. Реакции экстрактов, зелья, процессор, консоль, ресайклер, органы и лаборатория на карте **не сделаны**. Полный игровой цикл не гонялся — только сборка сервера, юнит-тест таблицы мутаций и YAML-линтер.

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
- [ ] Экстракт — каркас `be2c775c6f` (100 ед., uses 1..5, шприц, спрайт). **Реакций нет**
- [ ] Реакции экстрактов (серый plasma/water/blood и остальная таблица)
- [ ] Зелья (стабилизатор, мутатор, стероид, docility и т.д.)
- [ ] Процессор мёртвого слайма → экстракты по `Cores`
- [ ] Консоль, ресайклер, ручной сканер
- [ ] Обезьяньи кубы — прототипы `2a1fdca7b8`; спавн из серого экстракта не сделан
- [ ] Органы, диссекция, анализатор
- [ ] Лаборатория на карте (ящик, питание, без правок SunriseMapPool)
- [x] Сборка — `dotnet build Content.Server/Content.Server.csproj --configuration Debug --verbosity quiet --no-restore` — succeeded (после изменений в ветке)
- [x] YAML-линтер — `dotnet run --project Content.YAMLLinter/Content.YAMLLinter.csproj --configuration Release --no-build` — **No errors found** (~54 с; проверяет все прототипы, в т.ч. `Resources/Prototypes/_Fish/Xenobiology/slimes.yml`)
- [x] Хендофф — `Documentation/Porting/Xenobiology/HANDOFF.md`

### Известные упрощения (не закрыты)

- Огнетушитель, снимающий trained, не перенесён
- Полная кривая холода ниже −50°C — частично (blunt в life-тике есть, не весь паритет SS13)
- Скрещивание ядер, acidproof/explosion resistance зелий в SS220 — не в scope скачанных файлов слайма
