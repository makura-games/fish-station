using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Базовый маркер clockwork-структуры (доступ к сигилу Transmission и т.п.).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishClockworkStructureComponent : Component
{
    [DataField]
    public float ConstructionValue = 1f;

    [DataField]
    public bool RequiresPower;
}
