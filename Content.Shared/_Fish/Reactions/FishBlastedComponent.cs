using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Fish.Reactions;

/// <summary>
/// Маркер: сущность недавно отброшена взрывной волной — при ударе о стену = травма.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FishBlastedComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ThrowSpeed;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan Expires;
}
