using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Mechs.Components;

/// <summary>
/// Кабина: переключение внутреннего баллона поверх MechAir.
/// Работает вместе с server <c>MechAir</c>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechCabinAtmosComponent : Component
{
    /// <summary>
    /// true = пилот дышит из MechAir; false = из окружающей среды.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool UseInternalTank = true;

    [DataField]
    public EntProtoId ToggleAction = "ActionMechToggleInternals";

    [DataField]
    public EntityUid? ToggleActionEntity;
}
