using Content.Server.Silicons.Borgs;
using Content.Shared._Fish.BorgRest;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Components;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.GameObjects;

namespace Content.Server._Fish.BorgRest;

/// <summary>
/// Fish: серверная механика отдыха боргов Mk2 — DoAfter переключения, блок движения,
/// пониженный расход батареи и данные appearance для клиентского визуала.
/// Эффекты снимаются автоматически при смерти и при удалении компонента.
/// </summary>
public sealed partial class BorgRestSystem : SharedBorgRestSystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedHandheldLightSystem _handheldLight = default!;
    [Dependency] private BorgSystem _borg = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgRestComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BorgRestComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BorgRestComponent, BorgRestActionEvent>(OnRestAction);
        SubscribeLocalEvent<BorgRestComponent, BorgRestDoAfterEvent>(OnRestDoAfter);
        SubscribeLocalEvent<BorgRestComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    /// <summary>
    /// Выдаёт действие отдыха при появлении компонента — работает и при спавне Mk2
    /// (компонент из YAML), и при апгрейде обычного борга до Mk2 (компонент добавляется
    /// на живой сущности после MapInit, поэтому ActionGrant с его MapInit здесь не поможет).
    /// </summary>
    private void OnStartup(Entity<BorgRestComponent> ent, ref ComponentStartup args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.Action, RestActionId);
    }

    /// <summary>
    /// Убирает действие и снимает эффекты отдыха, если компонент удаляют прямо в отдыхе —
    /// иначе блок движения и расход остались бы с «протухшим» SavedDrawRate.
    /// </summary>
    private void OnShutdown(Entity<BorgRestComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.Action);
        UnapplyRest(ent);
    }

    /// <summary>
    /// Нажатие действия отдыха: роутер по схеме OnEvent -> TryDo -> CanDo -> Do
    /// (правило ss14-interaction-flow).
    /// </summary>
    private void OnRestAction(Entity<BorgRestComponent> ent, ref BorgRestActionEvent args)
    {
        if (args.Handled)
            return;

        if (TryToggleRest(ent))
            args.Handled = true;
    }

    /// <summary>
    /// Публичная попытка переключения состояния отдыха: проверяет условия
    /// (<see cref="CanToggleRest"/>) и при успехе запускает DoAfter переключения.
    /// </summary>
    public bool TryToggleRest(Entity<BorgRestComponent> ent)
    {
        if (!CanToggleRest(ent))
            return false;

        return DoToggleRest(ent);
    }

    /// <summary>
    /// Условия переключения: не идёт уже текущий DoAfter и борг жив.
    /// Чистая проверка без побочных эффектов (правило ss14-interaction-flow).
    /// </summary>
    public bool CanToggleRest(Entity<BorgRestComponent> ent)
    {
        // Повторные нажатия пока идёт DoAfter, и переход только у живого борга.
        return !_doAfter.IsRunning(ent.Comp.ActiveToggleDoAfter)
            && _mobState.IsAlive(ent.Owner);
    }

    /// <summary>
    /// Запуск DoAfter переключения; вызывается только после успешной проверки.
    /// </summary>
    private bool DoToggleRest(Entity<BorgRestComponent> ent)
    {
        var doAfterArgs = new DoAfterArgs(EntityManager, ent.Owner, ent.Comp.ToggleDelay,
            new BorgRestDoAfterEvent(), ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            // Переключение собственного состояния не требует возможности взаимодействовать
            // (действие отдыха имеет checkCanInteract: false): RequireCanInteract по умолчанию
            // гасит DoAfter на клиенте из-за BlockMovement, что рисует мигающую плашку.
            RequireCanInteract = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs, out var doAfterId))
            return false;

        ent.Comp.ActiveToggleDoAfter = doAfterId;
        return true;
    }

    /// <summary>
    /// Завершение DoAfter: независимо от исхода сбрасывает идентификатор активного
    /// переключения, при успехе инвертирует состояние отдыха.
    /// </summary>
    private void OnRestDoAfter(Entity<BorgRestComponent> ent, ref BorgRestDoAfterEvent args)
    {
        ent.Comp.ActiveToggleDoAfter = null;

        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.IsResting)
            UnapplyRest(ent);
        else
            ApplyRest(ent);
    }

    /// <summary>
    /// Выход из отдыха при любом состоянии, отличном от Alive: переключение в крите
    /// и в смерти не предусмотрено, а покоящийся спрайт не должен накладываться на wreck.
    /// </summary>
    private void OnMobStateChanged(Entity<BorgRestComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            UnapplyRest(ent);
    }

    /// <summary>
    /// Применяет эффекты отдыха: сеть-состояние, блок движения и расход, appearance-данные,
    /// выключение действий и фонаря.
    /// </summary>
    private void ApplyRest(Entity<BorgRestComponent> ent)
    {
        if (ent.Comp.IsResting || !_mobState.IsAlive(ent.Owner))
            return;

        ent.Comp.IsResting = true;
        Dirty(ent);

        ApplyRestMovement(ent);
        ApplyRestDrawRate(ent);
        ApplyRestActions(ent);
        ApplyRestModule(ent);

        // Фонарь гасится при входе в отдых, как в wreck: слой LightStatus скрыт,
        // а горящая лампа впустую жгла бы батарею (см. BorgNoFallSystem).
        // Исходное состояние запоминается, чтобы вернуть фонарь при выходе из отдыха.
        if (TryComp<HandheldLightComponent>(ent.Owner, out var light))
        {
            ent.Comp.SavedLight = light.Activated;

            if (light.Activated)
                _handheldLight.TurnOff((ent.Owner, light), makeNoise: false);
        }

        _appearance.SetData(ent.Owner, BorgRestVisuals.Resting, true);
    }

    /// <summary>
    /// Снимает эффекты отдыха; идемпотентно — вызывается из выхода, смерти и shutdown.
    /// </summary>
    private void UnapplyRest(Entity<BorgRestComponent> ent)
    {
        if (!ent.Comp.IsResting)
            return;

        ent.Comp.IsResting = false;
        Dirty(ent);

        // Удаляется только собственный блок: чужой BlockMovement при входе не перехватывался.
        if (ent.Comp.AddedBlockMovement && TryComp<BlockMovementComponent>(ent.Owner, out _))
            RemComp<BlockMovementComponent>(ent.Owner);
        ent.Comp.AddedBlockMovement = false;

        if (ent.Comp.SavedDrawRate is { } saved)
        {
            RestoreDrawRate(ent, saved);
            ent.Comp.SavedDrawRate = null;
        }

        RestoreRestActions(ent);
        RestoreRestModule(ent);
        RestoreLight(ent);

        _appearance.SetData(ent.Owner, BorgRestVisuals.Resting, false);
    }

    /// <summary>
    /// Ставит блок движения без блокировки рук: BlockInteraction/BlockUse = false
    /// оставляют подбор, использование и взаимодействия доступными — блокируется
    /// только перемещение (сам BlockMovementComponent всегда режет UpdateCanMoveEvent).
    /// Свой компонент не добавляется поверх чужого, чтобы не конфликтовать с другой механикой.
    /// </summary>
    private void ApplyRestMovement(Entity<BorgRestComponent> ent)
    {
        if (HasComp<BlockMovementComponent>(ent.Owner))
            return;

        var block = EnsureComp<BlockMovementComponent>(ent.Owner);
        block.BlockInteraction = false;
        block.BlockUse = false;
        ent.Comp.AddedBlockMovement = true;

        // Обязателен: без Dirty клиент получает BlockInteraction = true по умолчанию,
        // блокирует взаимодействия в предсказании и гасит DoAfter через RequireCanInteract.
        Dirty(ent.Owner, block);
    }

    /// <summary>
    /// Понижает расход батареи на время отдыха, запоминая исходный DrawRate шасси.
    /// Повторный вход не затирает запомненное значение уже пониженным.
    /// </summary>
    private void ApplyRestDrawRate(Entity<BorgRestComponent> ent)
    {
        if (!TryComp<PowerCellDrawComponent>(ent.Owner, out var draw))
            return;

        ent.Comp.SavedDrawRate ??= draw.DrawRate;

        _powerCell.SetDrawRate((ent.Owner, null), ent.Comp.RestDrawRate);
    }

    /// <summary>
    /// Возвращает исходный DrawRate шасси и пересчитывает заряд батареи.
    /// </summary>
    private void RestoreDrawRate(Entity<BorgRestComponent> ent, float saved)
    {
        _powerCell.SetDrawRate((ent.Owner, null), saved);
    }

    /// <summary>
    /// Выключает все действия борга, кроме действия отдыха: выключенные кнопки
    /// серые и не нажимаются (ActionButton рисует псевдокласс disabled от Enabled),
    /// в отличие от отмены в ActionAttemptEvent, когда кнопка успевает мигнуть.
    /// Отключённые кем-то ещё действия не запоминаются — при выходе не включаются.
    /// </summary>
    private void ApplyRestActions(Entity<BorgRestComponent> ent)
    {
        if (!TryComp<ActionsComponent>(ent.Owner, out var actions))
            return;

        foreach (var actionUid in actions.Actions)
        {
            if (!TryComp<ActionComponent>(actionUid, out var action) || !action.Enabled)
                continue;

            // Собственное действие отдыха остаётся доступным, чтобы выйти из отдыха.
            if (HasComp<BorgRestAllowedMarkerComponent>(actionUid))
                continue;

            _actions.SetEnabled(actionUid, false);
            ent.Comp.DisabledActions.Add(actionUid);
        }
    }

    /// <summary>
    /// Включает обратно только действия, выключенные при входе в отдых; удалённые
    /// за это время пропускаются.
    /// </summary>
    private void RestoreRestActions(Entity<BorgRestComponent> ent)
    {
        foreach (var actionUid in ent.Comp.DisabledActions)
        {
            if (Exists(actionUid))
                _actions.SetEnabled(actionUid, true);
        }

        ent.Comp.DisabledActions.Clear();
    }

    /// <summary>
    /// Складывает выдвинутый модуль на время отдыха: меню действий уже заблокировано
    /// (<see cref="ApplyRestActions"/>), но предмет, выданный выбранным модулем, оставался
    /// бы в руке и позволял отдыхающему боргу им пользоваться. Запоминаем выбор,
    /// чтобы вернуть его при выходе (см. <see cref="RestoreRestModule"/>).
    /// </summary>
    private void ApplyRestModule(Entity<BorgRestComponent> ent)
    {
        if (!TryComp<BorgChassisComponent>(ent.Owner, out var chassis) || chassis.SelectedModule == null)
            return;

        ent.Comp.SavedSelectedModule = chassis.SelectedModule;
        _borg.UnselectModule((ent.Owner, chassis));
    }

    /// <summary>
    /// Возвращает модуль, сложенный при входе в отдых. Мёртвому/критовому боргу модуль
    /// не выдвигается, а уже извлечённый или более не выбираемый модуль пропускается —
    /// так SelectModule не сыпет ошибками в лог.
    /// </summary>
    private void RestoreRestModule(Entity<BorgRestComponent> ent)
    {
        var module = ent.Comp.SavedSelectedModule;
        ent.Comp.SavedSelectedModule = null;

        if (module is not { } moduleUid
            || !_mobState.IsAlive(ent.Owner)
            || !TryComp<BorgChassisComponent>(ent.Owner, out var chassis)
            || !TryComp<BorgModuleComponent>(moduleUid, out var moduleComp)
            || !moduleComp.Installed
            || moduleComp.InstalledEntity != ent.Owner
            || !HasComp<SelectableBorgModuleComponent>(moduleUid))
        {
            return;
        }

        _borg.SelectModule((ent.Owner, chassis), moduleUid);
    }

    /// <summary>
    /// Возвращает фонарь в состояние до отдыха: включается только если он горел до
    /// входа в отдых и компонент всё ещё существует. Запись сбрасывается всегда.
    /// Мёртвому/критовому боргу фонарь не зажигается — при не-Alive выходе из отдыха
    /// его гасит BorgNoFallSystem, и он не должен светить на wreck-спрайт.
    /// </summary>
    private void RestoreLight(Entity<BorgRestComponent> ent)
    {
        var wasOn = ent.Comp.SavedLight == true;
        ent.Comp.SavedLight = null;

        if (!wasOn || !_mobState.IsAlive(ent.Owner))
            return;

        if (TryComp<HandheldLightComponent>(ent.Owner, out var light))
            _handheldLight.TurnOn(ent.Owner, (ent.Owner, light));
    }
}
