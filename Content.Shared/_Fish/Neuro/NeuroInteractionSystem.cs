using Content.Shared._Fish.MentalHealth;
using Content.Shared.Interaction.Events;
using Robust.Shared.GameObjects;

namespace Content.Shared._Fish.Neuro;

/// <summary>
/// Применяет ограничения взаимодействия при критической потере нейронов на клиенте и сервере.
/// </summary>
public sealed partial class NeuroInteractionSystem : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NeuroComponent, InteractionAttemptEvent>(OnInteractionAttempt);
    }

    /// <summary>
    /// Отменяет взаимодействия сущности при критически низком уровне нейронов.
    /// </summary>
    private void OnInteractionAttempt(Entity<NeuroComponent> ent, ref InteractionAttemptEvent args)
    {
        if (ent.Comp.Current < MentalHealthConstants.NeuroCritical)
            args.Cancelled = true;
    }
}
