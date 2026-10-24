using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Глобальный пул мощности и vitality культа (на entity game rule).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishClockworkPowerComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Power;

    [DataField, AutoNetworkedField]
    public float Vitality;

    [DataField, AutoNetworkedField]
    public bool ScriptUnlocked;

    [DataField, AutoNetworkedField]
    public bool ApplicationUnlocked;

    [DataField, AutoNetworkedField]
    public bool Heralded;

    [DataField, AutoNetworkedField]
    public bool RatvarAwakened;
}
