using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Stargazer — генерация мощности от «звёздного света» (рядом с космосом/окном).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishStargazerComponent : Component
{
    [DataField]
    public float PowerPerSecond = FishClockworkCultConstants.StargazerPowerPerSecond;

    [DataField]
    public float StarlightRange = 3f;

    [ViewVariables]
    public float Accumulator;
}
