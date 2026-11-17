using Robust.Shared.Serialization;

namespace Content.Shared._Fish.ObrCall;

[Serializable, NetSerializable]
public enum ObrCallUiKey : byte
{
    Key,
}

/// <summary>
/// Состояние консоли вызова ОБР.
/// </summary>
[Serializable, NetSerializable]
public sealed class ObrCallBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly bool IsPurchaseMode;
    public readonly bool ShowCryoOption;
    public readonly int StationBalance;
    public readonly string? StatusMessage;
    public readonly List<ObrCallTeamEntry> Teams;

    public ObrCallBoundUserInterfaceState(
        bool isPurchaseMode,
        bool showCryoOption,
        int stationBalance,
        string? statusMessage,
        List<ObrCallTeamEntry> teams)
    {
        IsPurchaseMode = isPurchaseMode;
        ShowCryoOption = showCryoOption;
        StationBalance = stationBalance;
        StatusMessage = statusMessage;
        Teams = teams;
    }
}

[Serializable, NetSerializable]
public sealed class ObrCallTeamEntry
{
    public readonly string PrototypeId;
    public readonly string Name;
    public readonly string? Description;
    public readonly int? Cost;
    public readonly bool Available;
    public readonly string? UnavailableReason;

    public ObrCallTeamEntry(
        string prototypeId,
        string name,
        string? description,
        int? cost,
        bool available,
        string? unavailableReason)
    {
        PrototypeId = prototypeId;
        Name = name;
        Description = description;
        Cost = cost;
        Available = available;
        UnavailableReason = unavailableReason;
    }
}

/// <summary>
/// Запрос вызова отряда с опциональной миссией.
/// </summary>
[Serializable, NetSerializable]
public sealed class ObrCallRequestMessage : BoundUserInterfaceMessage
{
    public readonly string TeamId;
    public readonly string Mission;
    public readonly bool SpawnInCryo;

    public ObrCallRequestMessage(string teamId, string mission, bool spawnInCryo = false)
    {
        TeamId = teamId;
        Mission = mission;
        SpawnInCryo = spawnInCryo;
    }
}
