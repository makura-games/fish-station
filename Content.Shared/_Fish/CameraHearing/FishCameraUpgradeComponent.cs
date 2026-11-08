using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.CameraHearing;

/// <summary>
/// Предмет, который при клике по камере даёт ей слух и сам расходуется.
/// </summary>
[RegisterComponent]
public sealed partial class FishCameraUpgradeComponent : Component
{
    [DataField]
    public LocId UpgradedPopup = "fish-camera-upgrade-done";

    [DataField]
    public LocId AlreadyUpgradedPopup = "fish-camera-upgrade-already";
}
