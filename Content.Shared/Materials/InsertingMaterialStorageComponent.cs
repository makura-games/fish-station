using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Materials;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true), AutoGenerateComponentPause] // Fish-edit: обновляем слой и при повторной загрузке
public sealed partial class InsertingMaterialStorageComponent : Component
{
    /// <summary>
    /// The time when insertion ends.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    [AutoPausedField]
    public TimeSpan EndTime;

    [ViewVariables, AutoNetworkedField]
    public Color? MaterialColor;

    // Fish-start: непрозрачность листа без учёта его кромки.
    [ViewVariables, AutoNetworkedField]
    public float FishIntakeOpacity = 1f;
    // Fish-end
}
