using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Заряд Kindle на слэбе (ближний стан).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishClockworkKindleChargeComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan ExpiresAt;

    [DataField]
    public float StunSeconds = 5f;

    [DataField]
    public float Range = 2f;
}
