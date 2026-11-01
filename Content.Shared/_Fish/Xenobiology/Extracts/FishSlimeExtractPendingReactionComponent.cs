using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Fish.Xenobiology.Extracts;

public enum FishSlimeExtractPendingKind : byte
{
    OilExplosion,
    OrangeFire,
    DarkBlueFreeze,
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
}
