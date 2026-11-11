using Content.Shared.Shuttles.BUIStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Artillery;

[Serializable, NetSerializable]
public sealed partial class BluespaceArtilleryConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public ArtilleryVector2 TargetCoordinates;
    public string ExplosionType;
    public float TotalIntensity;
    public float Slope;
    public float MaxIntensity;
    public bool PreviewEnabled;
    public bool IsLinked;
    public bool IsCharging;
    public bool IsOnCooldown;
    public float CooldownRemaining;
    public Dictionary<NetEntity, string> AvailableStations;
    public NetEntity? SelectedStation;
    public NavInterfaceState? NavState;

    public BluespaceArtilleryConsoleBoundUserInterfaceState(
        ArtilleryVector2 targetCoordinates,
        string explosionType,
        float totalIntensity,
        float slope,
        float maxIntensity,
        bool previewEnabled,
        bool isLinked,
        bool isCharging,
        bool isOnCooldown,
        float cooldownRemaining,
        Dictionary<NetEntity, string> availableStations,
        NetEntity? selectedStation,
        NavInterfaceState? navState)
    {
        TargetCoordinates = targetCoordinates;
        ExplosionType = explosionType;
        TotalIntensity = totalIntensity;
        Slope = slope;
        MaxIntensity = maxIntensity;
        PreviewEnabled = previewEnabled;
        IsLinked = isLinked;
        IsCharging = isCharging;
        IsOnCooldown = isOnCooldown;
        CooldownRemaining = cooldownRemaining;
        AvailableStations = availableStations;
        SelectedStation = selectedStation;
        NavState = navState;
    }
}