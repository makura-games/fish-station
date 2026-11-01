# Прогресс порта ксенобиологии

## Слаймы (секции 1–2)

Последний SHA: `f34a11319c`.

Есть: 22 цвета, малыш/взрослый, голод и пороги роста, деление на 4 по таблице SS220, смерть взрослого (малыш + бешеный малыш), присасывание (не более одного слайма на жертву), плазма (5 листов), вода (`rand(10,15) * объём`), огонь лечит, холод замедляет, EMP сбрасывает заряд.

Каркас экстракта в ветке (`be2c775c6f`), реакций пока нет — не мой шаг сегодня.

Сборка: `dotnet build Content.Server/Content.Server.csproj --configuration Debug --verbosity quiet --no-restore` — succeeded (после присоски).

Юнит: `dotnet test Content.Tests/Content.Tests.csproj --configuration Debug --filter FullyQualifiedName~FishSlimeMutationTest` — Passed 7, Failed 0. YAML-линтер не запускался. В игре не проверялось.
