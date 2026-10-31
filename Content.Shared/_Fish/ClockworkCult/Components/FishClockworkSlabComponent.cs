using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Слэб Clockwork Cult — UI и заряд писаний (Kindle и т.п.).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishClockworkSlabComponent : Component
{
    /// <summary>
    /// Активный заряд дальнего писания (kindle, manacles, compromise).
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public string? ActiveChargeScriptureId;

    [ViewVariables, AutoNetworkedField]
    public TimeSpan? ChargeExpiresAt;
}
