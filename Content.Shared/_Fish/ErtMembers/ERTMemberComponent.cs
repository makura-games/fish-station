using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.ERTMembers;

/// <summary>
/// This is used for tagging a mob as member of ERT/CBURN/DeathSquad.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ERTMemberComponent : Component
{
    /// <summary>
    /// Прототип фракционной иконки, отображаемой на HUD Nanotrasen.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<FactionIconPrototype> NanotrasenStatusIcon = "FishNanoTrasenFaction";
}
