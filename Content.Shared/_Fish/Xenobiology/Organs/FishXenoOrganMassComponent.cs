using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Xenobiology.Organs;

/// <summary>
/// Неопознанная масса до обработки обученным слаймом.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishXenoOrganMassComponent : Component
{
    [DataField, AutoNetworkedField]
    public string TrueOrganPrototype = "FishXenoOrganRegenerativeFish";
}
