namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Маркер точки на Reebe для Abscond / вторжения экипажа.
/// </summary>
[RegisterComponent]
public sealed partial class FishClockworkReebeMarkerComponent : Component
{
    [DataField]
    public bool ServantSpawn;

    [DataField]
    public bool CrewInvasion;

    [DataField]
    public bool AbscondTarget = true;
}
