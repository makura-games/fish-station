using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Marker для Vitality Matrix sigil (доп. данные поверх FishClockworkSigilComponent).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishVitalityMatrixComponent : Component
{
    [DataField]
    public float DrainPerSecond = 5f;

    [DataField]
    public float HealPerSecond = 5f;

    [DataField]
    public float ReviveVitalityCost = FishClockworkCultConstants.VitalityReviveCost;
}
