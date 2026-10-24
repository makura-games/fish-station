using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Ratvarian spear — генерирует vitality при ударах.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishRatvarianSpearComponent : Component
{
    [DataField]
    public float VitalityPerHit = 5f;

    [DataField]
    public bool BreakOnThrow = true;
}
