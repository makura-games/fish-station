using Content.Shared._Fish.Objectives;
using Content.Shared.Implants.Components;
using Content.Shared.Mind;
using Content.Shared.Trigger.Systems;

namespace Content.Server._Fish.Objectives;

/// <summary>
/// Засчитывает цель «выжить», когда игрок сам активирует имплантат, удаляющий владельца (например, редспейс-имплант).
/// </summary>
public sealed partial class SurviveOnImplantActivateSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        // Разум нужно пометить раньше триггера имплантата: тот удаляет владельца.
        SubscribeLocalEvent<SurviveOnImplantActivateComponent, ActivateImplantEvent>(
            OnActivateImplant,
            before: [typeof(TriggerOnActivateImplantSystem)]);
    }

    /// <summary>
    /// Помечает разум игрока, активировавшего имплантат.
    /// </summary>
    private void OnActivateImplant(Entity<SurviveOnImplantActivateComponent> ent, ref ActivateImplantEvent args)
    {
        if (!_mind.TryGetMind(args.Performer, out var mindId, out _))
            return;

        EnsureComp<ImplantSurvivedMindComponent>(mindId);
    }
}
