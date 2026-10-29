using Content.Server.Audio;
using Content.Shared.Administration.Logs;
using Content.Shared.Clothing.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Roles;
using Content.Shared._Fish.Silicons.Borgs;
using Content.Shared._Fish.Silicons.Borgs.Components;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Необходимо поскольку этот partial класс расширяет ванильный тип и должен сохранять исходный namespace несмотря на расположение файла в _Fish.
namespace Content.Server.Silicons.Borgs;

/// <summary>
/// Апгрейд обычного borgType до соответствующего Mk2 предметом BorgUpgradeModule.
/// Сущность при этом не пересоздаётся: меняется только <see cref="BorgSwitchableTypeComponent.SelectedBorgType"/>,
/// поэтому мозг, имя, установленные модули, инвентарь и runtime-состояние компонентов сохраняются.
/// </summary>
public sealed partial class BorgSwitchableTypeSystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private AmbientSoundSystem _ambientSound = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    // Имена chassis-рук совпадают с руками в borg_chassis_mk2.yml.
    private const string Mk2RightHandId = "hand_right";
    private const string Mk2LeftHandId = "hand_left";

    /// <summary>
    /// Компоненты, которые апгрейд должен применить к уже существующему боргу.
    /// Их значения живут только в borg_chassis_mk2.yml и в C# не дублируются.
    /// </summary>
    private static readonly string[] Mk2UpgradeComponents =
    [
        "MobThresholds",
        "PowerCellDraw",
        "FlashImmunity",
        // Борг не опрокидывается в крит/смерть и гасит фонарь (BorgNoFallSystem).
        "BorgNoFall",
        // Радиус обзора AI у Mk2 шире (7 вместо 3), значения живут в borg_chassis_mk2.yml.
        "StationAiVision",
        // Механика отдыха Mk2: действие, блок движения и пониженный расход в rest.
        "BorgRest",
    ];

    /// <summary>
    /// Шасси, которым дополнительная броня Mk2 положена и при апгрейде, и при спавне:
    /// только СБ и миротворец. Остальные варианты Mk2 остаются на штатной BorgArmorSilicon.
    /// </summary>
    private static readonly string[] Mk2ArmorUpgradeWhitelist =
    [
        "FishBorgChassisSecurityMk2",
        "FishBorgChassisPeaceMk2",
    ];

    /// <summary>
    /// Подписывает обработчики апгрейда модулем BorgUpgradeModule.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgUpgradeModuleComponent, AfterInteractEvent>(OnUpgradeInteract);
        SubscribeLocalEvent<BorgUpgradeModuleComponent, BorgUpgradeDoAfterEvent>(OnUpgradeDoAfter);
    }

    // Поток взаимодействия разбит по схеме OnEvent -> Try -> Can -> Do:
    // обработчики событий тонкие, условия — в Can*, побочные эффекты — в Do*.

    /// <summary>
    /// Правый клик апгрейд-модулем по боргу; помечает событие обработанным
    /// и запускает апгрейд через <see cref="TryStartUpgrade"/>.
    /// </summary>
    private void OnUpgradeInteract(Entity<BorgUpgradeModuleComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!HasComp<BorgSwitchableTypeComponent>(target))
            return; // Не борг — интеракцию отдаём дальше.

        // Помечаем событие обработанным сразу, как только цель опознана как борг:
        // неудачные проверки (уже Mk2, идёт другой апгрейд) показывают popup
        // и не должны прокидывать взаимодействие дальше.
        args.Handled = true;

        TryStartUpgrade(ent, target, args.User);
    }

    /// <summary>
    /// Завершение DoAfter апгрейда: гасит ambience, чистит активное взаимодействие
    /// и при успехе выполняет апгрейд через <see cref="TryFinishUpgrade"/>.
    /// </summary>
    private void OnUpgradeDoAfter(Entity<BorgUpgradeModuleComponent> ent, ref BorgUpgradeDoAfterEvent args)
    {
        // Звук глушим первым, до любой проверки — иначе он останется играть после отмены.
        _ambientSound.SetAmbience(ent.Owner, false);

        // Очистка состояния активного взаимодействия — независимо от исхода DoAfter:
        // и при отмене, и при завершении следующий апгрейд должен видеть null.
        if (args.Target is { } currentTarget
            && TryComp<BorgSwitchableTypeComponent>(currentTarget, out var currentSwitchable))
        {
            currentSwitchable.ActiveUpgradeDoAfter = null;
        }

        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = TryFinishUpgrade(ent, target, args.User);
    }

    /// <summary>
    /// Пытается запустить апгрейд борга до Mk2: проверяет условия через
    /// <see cref="CanStartUpgrade"/> и при успехе запускает DoAfter через <see cref="DoStartUpgrade"/>.
    /// </summary>
    /// <param name="item">Предмет-апгрейд.</param>
    /// <param name="target">Цель-борг.</param>
    /// <param name="user">Использующий игрок.</param>
    /// <returns>True, если DoAfter запущен.</returns>
    public bool TryStartUpgrade(Entity<BorgUpgradeModuleComponent> item, EntityUid target, EntityUid user)
    {
        if (!CanStartUpgrade(item, target, user))
            return false;

        return DoStartUpgrade(item, target, user);
    }

    /// <summary>
    /// Проверяет, можно ли начать апгрейд <paramref name="target"/> предметом <paramref name="item"/>:
    /// у цели выбран обычный (не Mk2) тип и по ней не запущен другой апгрейд.
    /// Чистая проверка без побочных эффектов.
    /// </summary>
    /// <param name="quiet">Не показывать popup-сообщения игроку.</param>
    public bool CanStartUpgrade(
        Entity<BorgUpgradeModuleComponent> item,
        EntityUid target,
        EntityUid user,
        bool quiet = false)
    {
        if (!TryComp<BorgSwitchableTypeComponent>(target, out var switchable))
            return false;

        if (!TryGetUpgradeTarget(switchable.SelectedBorgType, out _))
        {
            // Ещё не выбран тип, либо цель уже Mk2 (повторный апгрейд запрещён).
            if (!quiet)
            {
                _popup.PopupEntity(
                    Loc.GetString(switchable.SelectedBorgType == null
                        ? "borg-upgrade-invalid-target"
                        : "borg-upgrade-already-mk2"),
                    target, user);
            }

            return false;
        }

        // Дедупликация DoAfter работает per-user, поэтому от второго игрока с отдельным
        // предметом спасает только флаг на самой цели.
        if (_doAfter.IsRunning(switchable.ActiveUpgradeDoAfter))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("borg-upgrade-in-progress"), target, user);

            return false;
        }

        return true;
    }

    /// <summary>
    /// Запускает DoAfter апгрейда, записывает активное взаимодействие в
    /// <see cref="BorgSwitchableTypeComponent.ActiveUpgradeDoAfter"/> и включает звук предмета.
    /// При мгновенном завершении (InstantDoAfters) состояние уже обработано синхронным
    /// событием — запись ID и звук пропускаются.
    /// </summary>
    /// <returns>True, если DoAfter запущен или завершился мгновенно.</returns>
    private bool DoStartUpgrade(Entity<BorgUpgradeModuleComponent> item, EntityUid target, EntityUid user)
    {
        var doAfterArgs = new DoAfterArgs(EntityManager, user, item.Comp.Delay,
            new BorgUpgradeDoAfterEvent(), item.Owner, target: target, used: item.Owner)
        {
            NeedHand = true,
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs, out var doAfterId))
            return false;

        // При теге InstantDoAfters (например, admin ghost) TryStartDoAfter поднимает
        // событие синхронно и не хранит doAfter в DoAfterComponent — к этому моменту
        // OnUpgradeDoAfter уже очистил состояние (и, возможно, завершил апгрейд с удалением
        // модуля). Записывать ActiveUpgradeDoAfter и включать ambience в этом случае нельзя:
        // ID устаревший, а звук остался бы играть у завершённого действия. Для обычного
        // выполняющегося DoAfter IsRunning true и порядок записи ID/звука не меняется.
        if (!_doAfter.IsRunning(doAfterId))
            return true;

        if (TryComp<BorgSwitchableTypeComponent>(target, out var switchable))
            switchable.ActiveUpgradeDoAfter = doAfterId;

        _ambientSound.SetAmbience(item.Owner, true);
        return true;
    }

    /// <summary>
    /// Пытается завершить апгрейд после DoAfter: повторно проверяет актуальные условия через
    /// <see cref="CanFinishUpgrade"/> и при успехе выполняет апгрейд через <see cref="DoFinishUpgrade"/>.
    /// </summary>
    /// <param name="item">Предмет-апгрейд, завершивший DoAfter.</param>
    /// <param name="target">Цель-борг.</param>
    /// <param name="user">Инициатор апгрейда.</param>
    /// <returns>True, если апгрейд выполнен.</returns>
    public bool TryFinishUpgrade(Entity<BorgUpgradeModuleComponent> item, EntityUid target, EntityUid user)
    {
        if (!CanFinishUpgrade(target))
            return false;

        DoFinishUpgrade(item, target, user);
        return true;
    }

    /// <summary>
    /// Повторная проверка условий апгрейда на момент завершения DoAfter: цель могла быть
    /// удалена, сменить состояние или уже стать Mk2, пока DoAfter шёл.
    /// Чистая проверка без побочных эффектов.
    /// </summary>
    public bool CanFinishUpgrade(EntityUid target)
    {
        if (!TryComp<BorgSwitchableTypeComponent>(target, out var switchable))
            return false;

        // Повторная проверка на случай гонки: цель могла стать Mk2, пока шёл DoAfter.
        return TryGetUpgradeTarget(switchable.SelectedBorgType, out _);
    }

    /// <summary>
    /// Выполняет сам апгрейд: переключает тип, применяет компоненты, руки, инвентарь и броню Mk2
    /// и расходует предмет-апгрейд.
    /// </summary>
    private void DoFinishUpgrade(Entity<BorgUpgradeModuleComponent> item, EntityUid target, EntityUid user)
    {
        // Условия уже проверены в CanFinishUpgrade; повторно добираем компонент и цель
        // только для передачи в UpgradeToMk2.
        if (!TryComp<BorgSwitchableTypeComponent>(target, out var switchable)
            || !TryGetUpgradeTarget(switchable.SelectedBorgType, out var upgradeTarget))
        {
            return;
        }

        UpgradeToMk2((target, switchable), upgradeTarget, user, item.Owner);

        // Апгрейд успешен — предмет расходуется. При отмене сюда не доходим.
        QueueDel(item.Owner);
    }

    /// <summary>
    /// Определяет Mk2-вариант для текущего типа борга по конвенции именования: у типа X
    /// существует скрытый от меню Fish-тип FishXMk2. Проще и безопаснее явной таблицы
    /// соответствий — править чужие borgType-прототипы не приходится.
    /// </summary>
    private bool TryGetUpgradeTarget(ProtoId<BorgTypePrototype>? source, out ProtoId<BorgTypePrototype> target)
    {
        target = default;

        if (source is not { } sourceId)
            return false;

        var sourceStr = sourceId.ToString();

        // Пустую строку проверяем до индексации: TryIndex на "" всегда неудачен,
        // а кандидат Fish{char.ToUpperInvariant(sourceStr[0])}... бросил бы IndexOutOfRange.
        if (sourceStr.Length == 0
            || !ProtoMan.TryIndex(sourceStr, out BorgTypePrototype? sourceProto)
            || sourceProto.HideInMenu)
        {
            return false;
        }

        // Fish-прототипы форка обязаны иметь Fish-префикс: generic → FishGenericMk2.
        var candidate = $"Fish{char.ToUpperInvariant(sourceStr[0])}{sourceStr[1..]}Mk2";

        if (!ProtoMan.TryIndex(candidate, out BorgTypePrototype? targetProto) || !targetProto.HideInMenu)
            return false;

        target = candidate;
        return true;
    }

    /// <summary>
    /// Переключает борга на Mk2-тип на месте.
    /// </summary>
    private void UpgradeToMk2(
        Entity<BorgSwitchableTypeComponent> borg,
        ProtoId<BorgTypePrototype> target,
        EntityUid user,
        EntityUid item)
    {
        borg.Comp.SelectedBorgType = target;
        Dirty(borg);

        // Popup, footstep и SpriteMovement на сервере. Спрайт (state, RSI, offset) и
        // mind-состояния клиент применит сам после получения состояния компонента.
        UpdateEntityAppearance(borg);

        UpdateTransponder(borg, target);

        // Компоненты Mk2 (пороги здоровья, скорость разряда, флаш-иммунитет, радиус обзора AI)
        // и инвентарь (belt-слот + броня) применяются к живой сущности: сущность не
        // пересоздаём, поэтому мозг, имя, установленные модули, инвентарь и runtime-состояние
        // компонентов сохраняются.
        ApplyMk2Hands(borg.Owner);

        if (ProtoMan.TryIndex(target, out BorgTypePrototype? targetProto))
        {
            ApplyMk2Components(borg.Owner, targetProto);
            ApplyMk2Inventory(borg.Owner, targetProto);
        }

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{user} upgraded borg {borg.Owner} to {target} with {item}");

        _popup.PopupEntity(Loc.GetString("borg-upgrade-complete"), borg.Owner, user);
    }

    /// <summary>
    /// Постоянные chassis-руки есть только у Mk2-прототипов и риперов, а у
    /// BorgChassisSelectable их нет — при апгрейде добавляем их на живой сущности.
    /// Словарь рук сетевой, поэтому AddHand реплицируется клиенту автоматически;
    /// ShowInHands и CanBeStripped включаются здесь же и уезжают в HandsComponentState.
    /// </summary>
    private void ApplyMk2Hands(EntityUid uid)
    {
        if (!TryComp<HandsComponent>(uid, out var hands))
            return;

        // Guard: сущность могла уже получить руки из YAML-прототипа (spawn как Mk2).
        if (!_hands.TryGetHand((uid, hands), Mk2RightHandId, out _))
        {
            // AddHand сам сортирует SortedHands, делает Dirty и при null ActiveHandId
            // назначает активной первую добавленную руку (hand_right).
            _hands.AddHand((uid, hands), Mk2RightHandId, HandLocation.Right);
            _hands.AddHand((uid, hands), Mk2LeftHandId, HandLocation.Left);
        }

        // Апгрейд делает статические chassis-руки стрипаемыми в Strip-UI, как у Mk2-прототипов
        // (borg_chassis_mk2.yml). Ставим безусловно, в том числе когда руки уже пришли из YAML.
        _hands.SetCanBeStripped((uid, hands), true);
        _hands.SetShowInHands((uid, hands), true);
    }

    /// <summary>
    /// Применяет к живому боргу те компоненты, которые есть у соответствующего Mk2-chassis.
    /// Обычный борг уже имеет MobThresholds (0/100/300) и PowerCellDraw (0.6), поэтому простого
    /// добавления с overwrite: false недостаточно — значения Mk2 должны перекрыть старые.
    /// </summary>
    private void ApplyMk2Components(EntityUid uid, BorgTypePrototype prototype)
    {
        string dummyId = prototype.DummyPrototype;

        if (!ProtoMan.TryIndex<EntityPrototype>(dummyId, out var chassis))
            return;

        foreach (var key in Mk2UpgradeComponents)
        {
            if (!chassis.Components.TryGetValue(key, out var entry))
                continue;

            if (key == "PowerCellDraw" && HasComp<PowerCellDrawComponent>(uid))
            {
                // Перезапись прототипом сбросила бы Enabled и остановила разряд борга,
                // поэтому меняем только скорость разряда — это и есть Mk2-характеристика.
                _powerCell.ApplyPrototypeDrawRate((uid, null), entry);
                continue;
            }

            // MobThresholds перезаписывается целиком: ComponentStartup пересчитает состояние
            // и алерты по новым порогам Mk2. FlashImmunity у обычного борга отсутствует,
            // но overwrite делает вызов безопасным и для уже существующих случаев.
            EntityManager.AddComponent(uid, entry, overwrite: true);
        }
    }

    /// <summary>
    /// Переводит инвентарь апгрейднутого борга на Mk2-шаблон (в нём есть belt-слот) и
    /// заменяет BorgArmorSilicon на BorgArmorSiliconMk2 — но только для шасси из
    /// <see cref="Mk2ArmorUpgradeWhitelist"/>; остальные остаются на прежней броне.
    /// </summary>
    private void ApplyMk2Inventory(EntityUid uid, BorgTypePrototype prototype)
    {
        // SelectBorgModule при апгрейде не вызывается (он работает только на MapInit),
        // поэтому шаблон меняем явно. head/armor есть и в старом, и в новом шаблоне —
        // UpdateInventoryTemplate удалит только отсутствующие контейнеры, надетые вещи
        // и содержимое модулей не пострадают.
        if (TryComp<InventoryComponent>(uid, out var inventory))
            _inventorySystem.SetTemplateId((uid, inventory), prototype.InventoryTemplateId);

        ApplyMk2Armor(uid, prototype);
    }

    /// <summary>
    /// Снимает BorgArmorSilicon и надевает BorgArmorSiliconMk2 — тот же предмет, что получает
    /// свежеспавненный Mk2 через Loadout. Loadout здесь не срабатывает, потому что он
    /// привязан к MapInitEvent, а сущность не пересоздаётся.
    /// Для шасси вне <see cref="Mk2ArmorUpgradeWhitelist"/> — no-op.
    /// </summary>
    private void ApplyMk2Armor(EntityUid uid, BorgTypePrototype prototype)
    {
        // Броню Mk2 при апгрейде получают только Security и Peace —
        // остальные варианты Mk2 остаются на штатной BorgArmorSilicon.
        // Сравниваем строки (.Id): EntProtoId и string конвертируются друг в друга,
        // из-за чего Array.IndexOf выбрал бы не-generic перегрузку (Array, object)
        // и сравнивал бы строку с бокснутым EntProtoId (всегда false).
        if (Array.IndexOf(Mk2ArmorUpgradeWhitelist, prototype.DummyPrototype.Id) < 0)
            return;

        if (!TryGetMk2Armor(prototype, out var armorId))
            return;

        // BorgArmorSilicon помечен Unremoveable, поэтому снять его можно только через force.
        _inventorySystem.TryGetSlotEntity(uid, "armor", out var oldArmor);

        if (oldArmor != null && !_inventorySystem.TryUnequip(uid, "armor", out _, silent: true, force: true))
            return; // Снять не удалось — остаёмся на старой броне, без брони борг не остаётся.

        var newArmor = Spawn(armorId, Transform(uid).Coordinates);

        // TryEquip возвращает true только если предмет реально оказался в слоте, но на всякий
        // случай сверяем содержимое слота: борг не должен остаться без брони ни при каком исходе.
        var equipped = _inventorySystem.TryEquip(uid, newArmor, "armor", silent: true, force: true)
            && _inventorySystem.TryGetSlotEntity(uid, "armor", out var slotArmor)
            && slotArmor == newArmor;

        if (equipped)
        {
            if (oldArmor != null)
                QueueDel(oldArmor.Value);
            return;
        }

        // Не наделась: убираем новый предмет и возвращаем старую броню.
        QueueDel(newArmor);

        if (oldArmor != null)
            _inventorySystem.TryEquip(uid, oldArmor.Value, "armor", silent: true, force: true);
    }

    /// <summary>
    /// Достаёт id брони Mk2 из компонента Loadout dummy-прототипа — того же источника,
    /// из которого броню получает спавнящийся Mk2.
    /// </summary>
    private bool TryGetMk2Armor(BorgTypePrototype prototype, out EntProtoId armorId)
    {
        armorId = default;

        if (!ProtoMan.TryIndex<EntityPrototype>(prototype.DummyPrototype, out var chassis))
            return false;

        if (!chassis.Components.TryGetValue("Loadout", out var entry)
            || entry.Component is not LoadoutComponent loadout
            || loadout.StartingGear is not { Count: > 0 } gears
            || !ProtoMan.TryIndex(gears[0], out var gear))
        {
            return false;
        }

        var gearId = ((IEquipmentLoadout) gear).GetGear("armor");

        if (string.IsNullOrEmpty(gearId))
            return false;

        armorId = gearId;

        // Защита от опечатки в YAML: Spawn на несуществующий прототип дал бы невалидный EntityUid.
        return ProtoMan.TryIndex<EntityPrototype>(armorId, out _);
    }
}
