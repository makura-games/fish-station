using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Fish.Xenobiology.Extracts;

public enum FishSlimeExtractPendingKind : byte
{
    OilExplosion,
    OrangeFire,
    DarkBlueFreeze,
    GoldCritHostile,
    GoldCritLesser,
    GoldCritFriendly,
}

/// <summary>
/// Отложенный эффект реакции экстракта (взрыв, огонь, мороз).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class FishSlimeExtractPendingReactionComponent : Component
{
    [DataField, AutoNetworkedField]
    public FishSlimeExtractPendingKind Kind;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan TriggerAt;

    public EntityUid? SourceUser;

    /// <summary>
    /// Число мобов gold crit (Paradise: plasma 5 / blood 3 / water 1).
    /// </summary>
    [DataField, AutoNetworkedField]
    public int GoldCritCount;
}
