using Content.Shared._Sunrise.CollectiveMind;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Маркер слуги Ратвара.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishClockworkServantComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool HasArmaments;

    [DataField, AutoNetworkedField]
    public TimeSpan? NextArmorTime;

    [DataField, AutoNetworkedField]
    public TimeSpan? NextSpearTime;

    [DataField]
    public ProtoId<CollectiveMindPrototype> CollectiveMind = "FishClockworkCult";
}
