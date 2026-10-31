using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Привязанные арсеналы (armor + spear) + entity actions.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishClockworkArmamentsComponent : Component
{
    [DataField]
    public EntProtoId HelmetPrototype = "FishClothingHeadHelmetClockwork";

    [DataField]
    public EntProtoId ArmorPrototype = "FishClothingOuterArmorClockwork";

    [DataField]
    public EntProtoId GlovesPrototype = "FishClothingHandsGlovesClockwork";

    [DataField]
    public EntProtoId ShoesPrototype = "FishClothingShoesClockwork";

    [DataField]
    public EntProtoId SpearPrototype = "FishRatvarianSpear";

    [DataField]
    public EntProtoId CallArmorAction = "FishActionCallClockworkArmor";

    [DataField]
    public EntProtoId CallSpearAction = "FishActionCallRatvarianSpear";

    [DataField, AutoNetworkedField]
    public EntityUid? CallArmorActionEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? CallSpearActionEntity;
}
