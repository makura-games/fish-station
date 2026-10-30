using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Односторонний разлом / spatial gateway. Destination задаётся сервером.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishCityOfCogsRiftComponent : Component
{
    [DataField]
    public float LifetimeSeconds = 120f;

    [ViewVariables]
    public float RemainingLifetime = 120f;

    /// <summary>
    /// Цель телепорта (для Spatial Gateway и station→Reebe rifts).
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityCoordinates? Destination;

    /// <summary>
    /// Если true — это краткий Spatial Gateway (не Reebe invasion rift).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsSpatialGateway;
}
