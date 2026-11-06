using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.BorgRest;

/// <summary>
/// Fish: общий код механики отдыха боргов — блокировка действий во время отдыха.
/// Серверная часть (DoAfter, движение, расход, appearance) — в Content.Server._Fish.BorgRest.
/// </summary>
public abstract class SharedBorgRestSystem : EntitySystem
{
    /// <summary>
    /// Идентификатор действия отдыха.
    /// </summary>
    public static readonly EntProtoId RestActionId = "FishBorgRestAction";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActionComponent, ActionAttemptEvent>(OnActionAttempt);
    }

    /// <summary>
    /// Блокирует любые действия борга в состоянии отдыха, кроме собственно действия
    /// отдыха (маркер <see cref="BorgRestAllowedMarkerComponent"/>). Поднимается на
    /// самой action-сущности, поэтому для всех сущностей без BorgRestComponent
    /// обработчик — быстрый no-op без побочных эффектов.
    /// </summary>
    private void OnActionAttempt(Entity<ActionComponent> ent, ref ActionAttemptEvent args)
    {
        if (!TryComp<BorgRestComponent>(args.User, out var rest) || !rest.IsResting)
            return;

        if (HasComp<BorgRestAllowedMarkerComponent>(ent))
            return;

        args.Cancelled = true;
    }
}
