using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Mechs.Components;

/// <summary>
/// Давление влияет на скорость industrial-меха (Ripley/Clarke Lavaland-style).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MechPressureSpeedComponent : Component
{
    [DataField]
    public float LowPressureThreshold = 50f;

    [DataField]
    public float LowPressureMultiplier = 1.35f;

    [DataField]
    public float HighPressureMultiplier = 0.85f;
}
