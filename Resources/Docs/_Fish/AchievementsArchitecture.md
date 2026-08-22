# Архитектурный аудит системы достижений

## Результат аудита

В текущем коде отсутствует готовая система достижений, которую можно расширить. При этом основные инфраструктурные части уже существуют и должны быть переиспользованы:

- account-wide persistence через текущий `IServerDbManager`, `ServerDbBase` и `UserDbDataManager`;
- загрузка и кеширование данных по образцу `PlayTimeTrackingManager`;
- серверная обработка запросов и отправка состояния по образцу `RoadmapLikesSystem`;
- единый клиентский `UIController` и окно для Lobby и ESC;
- существующие popup/chat notification механизмы для уведомления о получении;
- YAML-прототипы для определений достижений и FTL для всего пользовательского текста.

Достижения не привязываются к character slot и не расширяют `PlayerPreferences`: владельцем прогресса является аккаунт `NetUserId`.

## Границы ответственности

### Определение достижения

Прототип хранит только декларативные данные:

- стабильный ID;
- локализуемые название и описание;
- категорию;
- секретность;
- тип прогресса и целевое значение;
- ключ условия;
- параметры общего условия;
- необязательную иконку.

Прототип не выполняет gameplay-логику и не определяет награды.

### Сервер

Сервер:

1. загружает состояние игрока после подключения;
2. подписывается на существующие gameplay- и round-events;
3. направляет событие только релевантным зарегистрированным условиям;
4. валидирует прогресс и разблокировку;
5. сохраняет изменение в существующей базе данных;
6. отправляет владельцу компактное изменение состояния и уведомление.

Клиентских сообщений для выдачи достижения не существует. Клиент может запросить только собственное отображаемое состояние.

### Клиент

Клиент:

- получает определения из локально загруженных прототипов;
- получает от сервера только собственные unlock/progress-данные;
- фильтрует и отображает категории;
- скрывает условие секретного достижения до получения;
- открывает одно и то же окно из Lobby и ESC;
- показывает серверное уведомление о разблокировке.

## Persistence

Основной вариант схемы — одна строка на пару `(PlayerUserId, AchievementId)`:

- `PlayerUserId`;
- `AchievementId`;
- `Progress`;
- `UnlockedAt`;
- `UpdatedAt`.

`UnlockedAt != null` является источником истины для бинарного состояния. Отдельные строки для заблокированных бинарных достижений не создаются.

Для SQLite и PostgreSQL создаются отдельные EF Core migrations текущим проектным инструментом. Методы чтения и записи добавляются через partial-файлы под `_Fish`, а изменения `Model.cs` ограничиваются моделью, `DbSet` и индексами.

Чтобы не терять прогресс и не создавать гонки между серверами:

- unlock записывается сразу;
- накопительный прогресс обновляется атомарно или сериализуется существующим DB command pipeline;
- reconnect повторно загружает состояние из базы;
- round end и disconnect выполняют flush незаписанного прогресса;
- повторная разблокировка идемпотентна;
- при **перманентном server-ban** (`ExpirationTime == null`) все строки `fish_achievement_progress` аккаунта удаляются, RAM-кеш сбрасывается (`AchievementBanCleanupSystem` → `ServerBanIssued`).

## Event-driven условия

Условия группируются по семействам событий, а не по отдельному handler на каждый прототип:

- round start/end;
- role и antagonist assignment;
- objective result;
- death и survival;
- damage, kill и weapon;
- interaction и item use;
- construction и crafting;
- medical и surgery;
- engineering;
- station events;
- shuttle и evacuation;
- exploration и location.

Каждое семейство индексирует прототипы по ключу условия и обрабатывает только соответствующую группу. Постоянного перебора всех достижений в `Update()` не будет.

Уникальные условия реализуются отдельными server-side handlers только после подтверждения, что существующее событие или общее условие не подходит.

## Антиабуз

Выдача идёт только через `AchievementManager.ContributeAsync` / force-admin:

- ghost и visiting mind блокируют прогресс;
- Admin Test Arena (`AdminTestArenaSystem` / `ATAM-*`) не фармит обычные ачивки;
- godmode origin не даёт heal/kill/damage progress;
- `EventKey` — одно реальное событие один раз за раунд (разные жертвы/предметы = разные ключи);
- без `EventKey` — `oncePerRound` + cooldown (survive и т.п.);
- mind.UserId должен совпадать с session;
- бинарные без params не открываются пачкой (`allowGenericTrigger`);
- клиент шлёт только `RequestAchievementsEvent` (свой snapshot);
- unique `(PlayerUserId, AchievementId)` в БД; unlocked — идемпотентный no-op;
- per-user lock против race duplicate async;
- reconnect в том же раунде не сбрасывает EventKey / once-per-round.

## UI и сеть

Один `AchievementUIController` управляет одним переиспользуемым окном.

- Lobby button вызывает `ToggleWindow()`.
- ESC button закрывает ESC menu и вызывает тот же `ToggleWindow()`.
- Первичная синхронизация происходит после завершения загрузки account data.
- Последующие unlock/progress изменения отправляются дельтами.
- Полный snapshot повторно запрашивается только при открытии окна после потери кеша или reconnect.

Для сотен записей окно по умолчанию открывает первую категорию (не «Все»).
В режиме «Все» скрываются `manual`-заглушки без прогресса, чтобы не строить ~500 контролов.
Сеть шлёт snapshot по запросу и дельты unlock/progress — не отдельное сообщение на каждый элемент каталога.

## Минимальные изменения core-файлов

Предварительно необходимы только следующие hooks:

1. `Content.Server.Database/Model.cs` — EF model, `DbSet` и индексы.
2. `Content.Server/IoC/ServerContentIoC.cs` — регистрация manager, если реализация не будет `EntitySystem`.
3. `Content.Client/_Sunrise/Lobby/UI/SunriseLobbyGui.xaml` и partial code-behind — отдельная Lobby button.
4. `Content.Client/Options/UI/EscapeMenu.xaml` — отдельная ESC button.
5. `Content.Client/UserInterface/Systems/EscapeMenu/EscapeUIController.cs` — вызов общего achievement controller.

Все новые shared/server/client классы, прототипы, локализация и тесты размещаются в `_Fish`.

При изменении оригинального C# используются маркеры:

```csharp
// ===== FISH EDIT START: ACHIEVEMENTS =====
// Изменение.
// ===== FISH EDIT END: ACHIEVEMENTS =====
```

## Неиспользуемые подходы

- `ObjectivesSystem` не является persistence-слоем и живёт в рамках раунда.
- `StatsBoardSystem` подходит как перечень событий, но его round-only состояние не становится хранилищем достижений.
- `PlayerPreferences` не используется для account-wide прогресса.
- EUI не используется только ради read-only списка, если обычный `UIController` с типизированными network events решает задачу проще.
- Компонент с сотнями networked fields не создаётся.

## Проверка архитектуры

До реализации контента должны быть покрыты тестами:

- идемпотентный unlock;
- накопительный progress и достижение порога;
- отсутствие client-to-server unlock API;
- persistence после reconnect;
- один snapshot и последующие delta updates;
- secret visibility;
- одинаковое окно из Lobby и ESC;
- корректная работа каталога из сотен определений.
