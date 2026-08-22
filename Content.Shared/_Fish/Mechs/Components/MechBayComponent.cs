using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Mechs.Components;

/// <summary>
/// Пол зарядки мехов (mech bay): при питании заряжает батарею меха на той же клетке.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MechBayComponent : Component
{
    [DataField]
    public float ChargeRate = 40f;

    [DataField]
    public float Range = 0.6f;
}
