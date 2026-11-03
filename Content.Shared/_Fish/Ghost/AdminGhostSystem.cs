using Content.Shared.Actions;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Shared._Fish.Ghost;

/// <summary>
/// Система <see cref="AdminGhostComponent"/>.
/// Даёт админскому призраку возможность запретить себе взаимодействие с миром.
/// </summary>
public sealed partial class AdminGhostSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private INetManager _net = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdminGhostComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AdminGhostComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<AdminGhostComponent, ToggleAghostInteractionActionEvent>(OnToggleInteraction);

        // Тот же набор действий, который запрещён обычным призракам через GhostComponent.CanGhostInteract.
        SubscribeLocalEvent<AdminGhostComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<AdminGhostComponent, UseAttemptEvent>(OnUseAttempt);
        SubscribeLocalEvent<AdminGhostComponent, PickupAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<AdminGhostComponent, DropAttemptEvent>(OnDropAttempt);
    }

    /// <summary>
    /// Выдаёт агосту кнопку самозапрета и выставляет её состояние.
    /// </summary>
    private void OnMapInit(Entity<AdminGhostComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.InteractionToggleActionEntity, ent.Comp.InteractionToggleAction);
        // Самозапрет по умолчанию включён, поэтому сразу показываем кнопку в активном состоянии.
        _actions.SetToggled(ent.Comp.InteractionToggleActionEntity, ent.Comp.InteractionRestricted);
        Dirty(ent.Owner, ent.Comp);
    }

    /// <summary>
    /// Убирает кнопку самозапрета вместе с компонентом.
    /// </summary>
    private void OnShutdown(Entity<AdminGhostComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.InteractionToggleActionEntity);
    }

    /// <summary>
    /// Обрабатывает нажатие кнопки самозапрета.
    /// </summary>
    private void OnToggleInteraction(Entity<AdminGhostComponent> ent, ref ToggleAghostInteractionActionEvent args)
    {
        if (args.Handled)
            return;

        // Само переключение происходит сразу, без задержки.
        if (!TryToggleInteraction((ent.Owner, ent.Comp)))
            return;

        // Кулдаун кнопки после нажатия: следующее нажатие доступно только через несколько секунд.
        // Задаём именно задержку использования, потому что сам кулдаун действия снимается сразу после выполнения.
        _actions.SetUseDelay(ent.Comp.InteractionToggleActionEntity, ent.Comp.ToggleCooldown);

        args.Handled = true;
    }

    /// <summary>
    /// Пытается переключить самозапрет на взаимодействие с миром в противоположное состояние.
    /// </summary>
    /// <returns><c>true</c>, если переключение разрешено и выполнено.</returns>
    public bool TryToggleInteraction(Entity<AdminGhostComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        if (!CanToggleInteraction((ent.Owner, ent.Comp)))
            return false;

        DoToggleInteraction((ent.Owner, ent.Comp));
        return true;
    }

    /// <summary>
    /// Проверяет, можно ли переключить самозапрет на взаимодействие. Состояние не меняет.
    /// </summary>
    public bool CanToggleInteraction(Entity<AdminGhostComponent?> ent)
    {
        return Resolve(ent, ref ent.Comp)
               && CanSetInteractionRestricted(ent, !ent.Comp.InteractionRestricted);
    }

    /// <summary>
    /// Переключает самозапрет на взаимодействие без проверок.
    /// </summary>
    private void DoToggleInteraction(Entity<AdminGhostComponent> ent)
    {
        // Состояние меняют и клиент (предсказание), и сервер. Сервер потом присылает своё значение через синхронизацию.
        SetInteractionRestricted(ent, !ent.Comp.InteractionRestricted);
    }

    /// <summary>
    /// Запрещает взаимодействовать с объектами мира и общее взаимодействие без цели
    /// (например, строительство), пока действует самозапрет.
    /// </summary>
    private void OnInteractionAttempt(Entity<AdminGhostComponent> ent, ref InteractionAttemptEvent args)
    {
        // Взаимодействие с самим собой (Target == ent.Owner, например собственные BUI консолей) запрет блокировать
        // не должен, иначе агост не сможет пользоваться консолями.
        // Кнопки агоста (в том числе переключатель самозапрета) используют checkCanInteract: false и сюда не попадают.
        // Проверку без цели (Target == null) пропускать нельзя: через неё проходит, например, строительство.
        if (args.Target == ent.Owner)
            return;

        if (ent.Comp.InteractionRestricted)
            args.Cancelled = true;
    }

    /// <summary>
    /// Запрещает использовать предметы, пока действует самозапрет.
    /// </summary>
    private void OnUseAttempt(Entity<AdminGhostComponent> ent, ref UseAttemptEvent args)
    {
        CancelIfRestricted(ent, args);
    }

    /// <summary>
    /// Запрещает подбирать предметы, пока действует самозапрет.
    /// </summary>
    private void OnPickupAttempt(Entity<AdminGhostComponent> ent, ref PickupAttemptEvent args)
    {
        CancelIfRestricted(ent, args);
    }

    /// <summary>
    /// Запрещает выбрасывать предметы, пока действует самозапрет.
    /// </summary>
    private void OnDropAttempt(Entity<AdminGhostComponent> ent, ref DropAttemptEvent args)
    {
        CancelIfRestricted(ent, args);
    }

    /// <summary>
    /// Отменяет событие, если самозапрет на взаимодействие включён.
    /// </summary>
    private void CancelIfRestricted(Entity<AdminGhostComponent> ent, CancellableEntityEventArgs args)
    {
        if (ent.Comp.InteractionRestricted)
            args.Cancel();
    }

    /// <summary>
    /// Пытается включить или выключить самозапрет на взаимодействие с миром.
    /// </summary>
    public bool TrySetInteractionRestricted(Entity<AdminGhostComponent?> ent, bool value)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        if (!CanSetInteractionRestricted((ent.Owner, ent.Comp), value))
            return false;

        SetInteractionRestricted((ent.Owner, ent.Comp), value);
        return true;
    }

    /// <summary>
    /// Проверяет, можно ли изменить состояние самозапрета на взаимодействие.
    /// </summary>
    public bool CanSetInteractionRestricted(Entity<AdminGhostComponent?> ent, bool value)
    {
        return Resolve(ent, ref ent.Comp)
               && ent.Comp.InteractionRestricted != value;
    }

    /// <summary>
    /// Включает или выключает самозапрет на взаимодействие с миром.
    /// </summary>
    private void SetInteractionRestricted(Entity<AdminGhostComponent> ent, bool value)
    {
        ent.Comp.InteractionRestricted = value;
        Dirty(ent.Owner, ent.Comp);

        _actions.SetToggled(ent.Comp.InteractionToggleActionEntity, value);

        // Всплывающее сообщение только на сервере, иначе оно задвоится при предсказании на клиенте.
        if (!_net.IsServer)
            return;

        var message = Loc.GetString(value
            ? "admin-ghost-interaction-restricted-on"
            : "admin-ghost-interaction-restricted-off");

        _popup.PopupEntity(message, ent.Owner, ent.Owner);
    }
}
