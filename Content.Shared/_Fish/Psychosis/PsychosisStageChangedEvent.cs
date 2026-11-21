using Robust.Shared.GameObjects;

namespace Content.Shared._Fish.Psychosis;

/// <summary>
/// Локальное событие сервера: стадия психоза сущности изменилась.
/// </summary>
public sealed class PsychosisStageChangedEvent : EntityEventArgs
{
    public readonly EntityUid Entity;
    public readonly PsychosisStage OldStage;
    public readonly PsychosisStage NewStage;
    public readonly float Level;

    public PsychosisStageChangedEvent(EntityUid entity, PsychosisStage oldStage, PsychosisStage newStage, float level)
    {
        Entity = entity;
        OldStage = oldStage;
        NewStage = newStage;
        Level = level;
    }
}
