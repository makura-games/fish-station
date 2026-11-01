# Прогресс порта ксенобиологии

## Слаймы (секции 1–2)

Последний SHA: `0fba437668` (паралич при T0C−40).

Есть: 22 цвета, малыш/взрослый, голод и пороги роста, деление на 4 по таблице SS220, смерть взрослого (малыш + бешеный малыш), присасывание (один слайм на жертву), плазма (5 листов), вода (`rand(10,15) * объём`), огонь лечит, холод (замедление + паралич пока ≤ T0C−40), EMP сбрасывает заряд.

Каркас экстракта в ветке (`be2c775c6f`), реакций нет.

Сборка: `dotnet build Content.Server/Content.Server.csproj --configuration Debug --verbosity quiet --no-restore` — succeeded.

Юнит: `dotnet test Content.Tests/Content.Tests.csproj --configuration Debug --filter FullyQualifiedName~FishSlimeMutationTest` — Passed 7, Failed 0.

YAML: `dotnet run --project Content.YAMLLinter/Content.YAMLLinter.csproj --configuration Release --no-build` — No errors found (~54 с). В игре не проверялось.
