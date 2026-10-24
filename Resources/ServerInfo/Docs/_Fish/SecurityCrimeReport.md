# Security Crime Report

Автор: AndreySmirnov  
Ветка: `feature/sec-quick-crime-report`  
PR: https://github.com/space-sunrise/fish-station/pull/313

Быстрый доклад статьи Fish Space Law с газовой маски СБ в канал Security (и спецканалы гарнитуры, если есть).

## Поведение

1. На маске: `SecurityCrimeReport` + `AccessReader` (`Security`).
2. Пока маска в слоте `mask` и есть доступ — в Hotbar Action `ActionSecurityCrimeReport`.
3. Action открывает `SimpleRadialMenu` со списком статей из компонента.
4. Выбор шлёт `SecurityCrimeReportSelectedEvent` на сервер.
5. Сервер проверяет: маска надета, статья из списка, доступ, cooldown, EMP.
6. Сообщение уходит в радио и messenger. Слушателям — `ChatNotification`.
7. После успешной отправки — cooldown 30 с (`UseDelay` id `security-crime-report` на носителе + cooldown Action).

Открытие меню cooldown не ставит.

## Статьи в меню

`LawFish101`, `201`, `301`, `312`, `302`, `303`, `304`, `401`, `502`.

Подкрепление (`RequiresReinforcement`): все `4xx`/`5xx`, плюс `300`, `301`, `310`, `312`.

## Каналы

Всегда `Security`. Плюс каналы с гарнитуры, которых нет у `EncryptionKeyStationMaster` (CentCom / ERT / DeathSquad и т.п.). Имена подразделений не хардкодятся.

## EMP

- Обычная `ClothingMaskGasSecurity`: при `EmpDisabled` на маске отправка отменяется (`radioSource` = маска). После снятия EMP до конца `MalfunctionUntil` (60 с) — доклад без локации, название с помехами.
- Маски CentCom / ERT (DeathSquad наследует) и спецгарнитуры ЦК/ОБР/ДСО/SpecOps: `EmpResistance` с `strengthMultiplier: 0` (как `ClothingHeadsetNinja`). EMP attempt отменяется — доклад и рация без soft-malfunction.
- BlueShield и обычные станционные гарнитуры не трогались.
- `EmpSystem` / `RadioSystem` не менялись. `TelecomExempt` не использовался.

## Файлы

Новые:

- `Content.Client/_Fish/SecurityCrimeReport/SecurityCrimeReportSystem.cs`
- `Content.Server/_Fish/SecurityCrimeReport/SecurityCrimeReportSystem.cs`
- `Content.Shared/_Fish/SecurityCrimeReport/*`
- `Resources/Prototypes/_Fish/Actions/security_crime_report.yml`
- `Resources/Prototypes/_Fish/Chat/security_crime_report_notifications.yml`
- `Resources/Locale/ru-RU/_strings/_fish/security/security-crime-report.ftl`
- `Resources/Locale/en-US/_strings/_fish/security/security-crime-report.ftl`
- этот файл

Изменены:

- `Resources/Prototypes/Entities/Clothing/Masks/masks.yml` — Security / CentCom / ERT
- гарнитуры CentCom / ERT / DeathSquad / SpecOps (vanilla, `_Sunrise`, `_Fish`)

## Ограничения

- Не весь закон, только shortlist в компоненте.
- Локация с nav beacon; без маяков — fallback.
- Messenger нужен server на станции; радио уходит и без него.
- Канал Security зависит от telecom; CentCom/ERT/DeathSquad — `longRange`.
- Popup отсутствия доступа — штатный `lock-comp-has-user-access-fail`.

## Что проверить

- Надеть/снять маску СБ, доступ / без доступа.
- Выбор статьи: код, название, локация в Security (+ messenger).
- Подкрепление на тяжёлых статьях, звук notification.
- Cooldown 30 с, обход второй маской / re-equip не работает.
- EMP на обычной маске СБ vs маске/гарнитуре ЦК/ОБР/ДСО.
- Обычная гарнитура СБ — только Security; спецгарнитура — Security + спецканал.
