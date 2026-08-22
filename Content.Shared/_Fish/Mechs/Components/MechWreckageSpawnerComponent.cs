using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Mechs.Components;

/// <summary>
/// При уничтожении/крите оставляет salvage-обломок с возможностью изъять остатки.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MechWreckageSpawnerComponent : Component
{
    [DataField]
    public EntProtoId WreckagePrototype = "MechWreckage";
}
