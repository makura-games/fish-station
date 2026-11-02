using Content.Shared._Fish.Xenobiology;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Xenobiology;

/// <summary>Одно запланированное срабатывание уже израсходованного экстракта.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class FishXenobiologyReactionComponent : Component
{
    public ProtoId<FishXenobiologyReactionPrototype> Recipe;

    [AutoPausedField]
    public TimeSpan FinishAt;

    public GameTick StartedTick;
    public EntityUid? User;

    /// <summary>Следующее изменение раствора закрывает окно привязки к завершённому переносу.</summary>
    public bool AwaitingTransfer;
}
