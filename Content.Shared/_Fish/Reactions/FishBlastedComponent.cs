using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Fish.Reactions;

/// <summary>
/// Маркер: сущность недавно отброшена взрывной волной — при ударе о стену = травма.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FishBlastedComponent : Component
{
    /// <summary>
    /// Скорость броска от взрыва, тайлы в секунду. Масштабирует тяжесть удара о стену.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ThrowSpeed;

    /// <summary>
    /// Момент, после которого серверный Update снимает компонент. Удар о стену снимает его раньше.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan Expires;
}
