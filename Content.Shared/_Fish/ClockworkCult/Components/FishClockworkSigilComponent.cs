using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Сигил Clockwork Cult.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishClockworkSigilComponent : Component
{
    [DataField, AutoNetworkedField]
    public FishClockworkSigilType SigilType = FishClockworkSigilType.Transgression;

    /// <summary>
    /// Локальный запас энергии для Transmission sigil.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StoredPower;

    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> OccupantEnterTimes = new();
}
