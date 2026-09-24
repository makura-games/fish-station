using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Leash.Components;

[RegisterComponent]
public sealed partial class CollarComponent : Component
{
    [DataField]
    public TimeSpan BreakoutTime = TimeSpan.FromSeconds(4);

    [DataField]
    public ProtoId<AlertPrototype> Alert = "Collared";
	
    [DataField]
    public float RemoveSuccessChance = 1f;

    public EntityUid? Wearer;
    public EntityUid? AttachedLeash;
}
