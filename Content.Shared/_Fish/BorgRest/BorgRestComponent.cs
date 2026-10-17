using Content.Shared.DoAfter;
using Robust.Shared.GameStates;

namespace Content.Shared._Fish.BorgRest;

/// <summary>
/// Fish: механика отдыха боргов Mk2. Пока борг отдыхает, его движение заблокировано,
/// расход батареи понижен до <see cref="RestDrawRate"/>, а действия кроме собственного
/// действия отдыха — заблокированы (см. SharedBorgRestSystem). При выходе и при
/// состоянии, отличном от Alive, эффекты снимаются автоматически сервером.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BorgRestComponent : Component
{
    /// <summary>
    /// Борг сейчас отдыхает. Сетевое состояние: клиентский визуал читает его
    /// из appearance (BorgRestVisuals.Resting), а не напрямую из компонента.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsResting;

    /// <summary>
    /// Скорость разряда батареи во время отдыха, в ваттах. Фактическая экономия —
    /// разница с исходным DrawRate шасси: она вычитается из скорости заряда батареи
    /// тем же путём, что и обычный PowerCellDraw.
    /// </summary>
    [DataField]
    public float RestDrawRate = 0.1f;

    /// <summary>
    /// Исходный DrawRate шасси, запомненный при входе в отдых; восстанавливается
    /// при выходе. <c>null</c> — отдых не активен (или запись уже восстановлена).
    /// </summary>
    [DataField]
    public float? SavedDrawRate;

    /// <summary>
    /// Задержка DoAfter входа/выхода из отдыха.
    /// </summary>
    [DataField]
    public TimeSpan ToggleDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Действие отдыха, выданное этой механикой; удаляется при удалении компонента.
    /// </summary>
    public EntityUid? Action;

    /// <summary>
    /// Активный DoAfter переключения; защищает от повторных нажатий пока идёт.
    /// </summary>
    public DoAfterId? ActiveToggleDoAfter;

    /// <summary>
    /// BlockMovementComponent был добавлен именно этой механикой и должен быть
    /// удалён при выходе; чужой компонент при входе не перехватывается.
    /// </summary>
    public bool AddedBlockMovement;

    /// <summary>
    /// Действия, выключенные этой механикой при входе в отдых (Enabled = false).
    /// При выходе включаются обратно только они — уже выключенные кем-то другим
    /// действия не включаются. Серверная запись, клиенту не реплицируется.
    /// </summary>
    public List<EntityUid> DisabledActions = new();
}
