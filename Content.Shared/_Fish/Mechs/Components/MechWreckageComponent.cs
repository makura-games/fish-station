using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Mechs.Components;

/// <summary>
/// Обломок меха: crowbar извлекает battery/модули (ограниченно).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechWreckageComponent : Component
{
    [DataField, AutoNetworkedField]
    public int SalvageLeft = 3;

    [DataField]
    public float SalvageDelay = 3f;
}
