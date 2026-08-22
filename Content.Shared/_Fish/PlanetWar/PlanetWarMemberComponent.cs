using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.PlanetWar;

/// <summary>
/// Участник PlanetWar: фракционная иконка статуса (вместо чужих AssaultOperative / PirateIcon).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlanetWarMemberComponent : Component
{
    [DataField]
    public ProtoId<FactionIconPrototype> StatusIcon = "FactionIconCoreBase";
}
