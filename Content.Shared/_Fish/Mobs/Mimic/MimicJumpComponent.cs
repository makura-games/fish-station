using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Mobs.Mimic;

[RegisterComponent, NetworkedComponent]
public sealed partial class MimicJumpComponent : Component
{
    [DataField]
    public EntProtoId ActionId = "FishActionMimicJump";

    [DataField]
    public float MaxJumpDistance = 10f;

    [DataField]
    public float JumpSpeed = 7f;

    [DataField]
    public float PushbackRatio = 10f;

    [DataField]
    public float ParalyzeTime = 5f;

    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "Slash", 15 },
        },
    };

    public bool Awake;

    public EntityUid? JumpAction;

    public bool Jumping;
}
