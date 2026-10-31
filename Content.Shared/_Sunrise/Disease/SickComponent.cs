// Fish-edit: To remove - функционал разумной болезни
using Content.Shared.Chemistry.Components;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class SickComponent : Component
{
    [DataField("owner")]
    [ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public EntityUid owner;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("icon")]
    public ProtoId<StatusIconPrototype> Icon = "SmartDiseaseIcon";

    [DataField("inited")]
    [AutoNetworkedField]
    public bool Inited = false;

    [DataField] public int Stady = 0;

    [DataField] public List<string> Symptoms = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextStadyAt = TimeSpan.Zero;

    [DataField("stadyDelay")]
    public TimeSpan StadyDelay = TimeSpan.FromMinutes(5);

    [DataField("beforeInfectedBloodReagents")]
    public Solution BeforeInfectedBloodReagents = new();
}
