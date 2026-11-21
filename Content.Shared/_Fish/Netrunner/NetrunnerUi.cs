using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Netrunner;

[Serializable, NetSerializable]
public enum NetrunnerUiKey : byte
{
    Terminal,
    Shop,
}

/// <summary>A camera identifier revealed by the server, without an entity PVS override.</summary>
[Serializable, NetSerializable]
public sealed record NetrunnerCameraEntry(NetEntity Entity, string Name);

/// <summary>A round-relative journal record. Device audits deliberately omit the perpetrator.</summary>
[Serializable, NetSerializable]
public sealed record NetrunnerLogEntry(TimeSpan Time, NetrunnerHackKind Kind, string Target, bool Success);

/// <summary>Private terminal snapshot sent only to an authorized implant holder.</summary>
[Serializable, NetSerializable]
public sealed class NetrunnerUiState : BoundUserInterfaceState
{
    public float Ram;
    public float MaxRam;
    public float Trace;
    public float Cooldown;
    public bool Connected;
    public bool Connecting;
    public float CastRemaining;
    public int NeuralLinks;
    public int UnlockedCount;
    public bool DebugMode;
    public bool VisorEquipped;
    public float BodyTemperature;
    public bool HasBodyTemperature;
    public List<NetrunnerCameraEntry> Cameras = new();
    public List<NetrunnerLogEntry> Journal = new();
}

[Serializable, NetSerializable]
public sealed class NetrunnerSelectCameraMessage(NetEntity camera) : BoundUserInterfaceMessage
{
    public NetEntity Camera = camera;
}

[Serializable, NetSerializable]
public sealed class NetrunnerRefreshMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class NetrunnerDisconnectMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class NetrunnerOpenShopMessage : BoundUserInterfaceMessage;

/// <summary>One purchase type, with prices and limits validated by the server.</summary>
[Serializable, NetSerializable]
public enum NetrunnerUpgrade : byte
{
    Memory,
    ActivationSpeed,
}

/// <summary>One catalog item visible to an authorized implant holder.</summary>
[Serializable, NetSerializable]
public sealed record NetrunnerShopEntry(ProtoId<NetrunnerAbilityPrototype> Ability,
    NetrunnerHackKind Kind, int Price, bool Owned, float RamCost, float TraceCost,
    float CastTime, float Duration, float Cooldown);

/// <summary>Private shop snapshot with current prices, balance and hardware limits.</summary>
[Serializable, NetSerializable]
public sealed class NetrunnerShopState : BoundUserInterfaceState
{
    public int NeuralLinks;
    public float MaxRam;
    public int MemoryLevel;
    public int MaxMemoryLevel;
    public int MemoryPrice;
    public float MemoryGain;
    public int SpeedLevel;
    public int MaxSpeedLevel;
    public int SpeedPrice;
    public float CastMultiplier;
    public float SpeedGain;
    public bool CanBuy;
    public bool DebugMode;
    public int VisorPrice;
    public bool VisorPurchased;
    public List<NetrunnerShopEntry> Programs = new();
}

[Serializable, NetSerializable]
public sealed class NetrunnerBuyProgramMessage(ProtoId<NetrunnerAbilityPrototype> ability) : BoundUserInterfaceMessage
{
    public ProtoId<NetrunnerAbilityPrototype> Ability = ability;
}

[Serializable, NetSerializable]
public sealed class NetrunnerBuyUpgradeMessage(NetrunnerUpgrade upgrade) : BoundUserInterfaceMessage
{
    public NetrunnerUpgrade Upgrade = upgrade;
}

[Serializable, NetSerializable]
public sealed class NetrunnerBuyVisorMessage : BoundUserInterfaceMessage;
