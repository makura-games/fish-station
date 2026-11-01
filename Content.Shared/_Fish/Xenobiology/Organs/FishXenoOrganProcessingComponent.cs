using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Fish.Xenobiology.Organs;

/// <summary>
/// Слайм переваривает органическую массу в орган.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FishXenoOrganProcessingComponent : Component
{
    [DataField, AutoNetworkedField]
    public string TrueOrganPrototype = "FishXenoOrganRegenerativeFish";

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan FinishAt;
}
