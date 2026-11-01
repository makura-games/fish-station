using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Research.Bluespace;

[Serializable, NetSerializable]
public enum FishBluespaceDeviceUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum FishBluespaceDeviceKind : byte
{
    Harvester,
    Prober,
    Suppressor,
}

[Serializable, NetSerializable]
public enum FishBluespacePowerMode : byte
{
    Normal,
    Overdrive,
}

[Serializable, NetSerializable]
public enum FishBluespaceLootCategory : byte
{
    Materials,
    Supplies,
    Research,
}

[Serializable, NetSerializable]
public enum FishBluespaceDeviceVisuals : byte
{
    Working,
    Unstable,
}

[Serializable, NetSerializable]
public enum FishBluespaceDeviceLayers : byte
{
    Base,
    Status,
}

/// <summary>Reports authoritative operating conditions for a bluespace infrastructure device.</summary>
[Serializable, NetSerializable]
public sealed class FishBluespaceDeviceBoundUserInterfaceState(
    FishBluespaceDeviceKind kind,
    bool enabled,
    bool powered,
    bool anchored,
    bool hasStation,
    bool hasResearchServer,
    bool critical,
    float background,
    float receivedPower,
    float requestedPower,
    float progress,
    float pointsPerMinute,
    FishBluespacePowerMode powerMode,
    FishBluespaceLootCategory lootCategory,
    bool outputBlocked = false,
    float energyPerReward = 0f) : BoundUserInterfaceState
{
    public readonly FishBluespaceDeviceKind Kind = kind;
    public readonly bool Enabled = enabled;
    public readonly bool Powered = powered;
    public readonly bool Anchored = anchored;
    public readonly bool HasStation = hasStation;
    public readonly bool HasResearchServer = hasResearchServer;
    public readonly bool Critical = critical;
    public readonly float Background = background;
    public readonly float ReceivedPower = receivedPower;
    public readonly float RequestedPower = requestedPower;
    public readonly float Progress = progress;
    public readonly float PointsPerMinute = pointsPerMinute;
    public readonly FishBluespacePowerMode PowerMode = powerMode;
    public readonly FishBluespaceLootCategory LootCategory = lootCategory;
    public readonly bool OutputBlocked = outputBlocked;
    public readonly float EnergyPerReward = energyPerReward;
}

/// <summary>Requests an explicit operating state; all machine conditions are checked on the server.</summary>
[Serializable, NetSerializable]
public sealed class FishBluespaceSetEnabledMessage(bool enabled) : BoundUserInterfaceMessage
{
    public readonly bool Enabled = enabled;
}

/// <summary>Selects the harvester's bounded power mode.</summary>
[Serializable, NetSerializable]
public sealed class FishBluespaceSetPowerModeMessage(FishBluespacePowerMode mode) : BoundUserInterfaceMessage
{
    public readonly FishBluespacePowerMode Mode = mode;
}

/// <summary>Selects a configured harvester reward category.</summary>
[Serializable, NetSerializable]
public sealed class FishBluespaceSetLootCategoryMessage(FishBluespaceLootCategory category) : BoundUserInterfaceMessage
{
    public readonly FishBluespaceLootCategory Category = category;
}
