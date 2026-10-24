using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Shipyard;

[Serializable, NetSerializable]
public enum ShipyardConsoleUiKey : byte
{
    Shipyard,
}

[Serializable, NetSerializable]
public sealed class ShipyardConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly int Balance;
    public readonly string? StatusMessage;
    public readonly bool Purchasing;
    public readonly List<ShipyardVesselEntry> Vessels;

    public ShipyardConsoleBoundUserInterfaceState(
        int balance,
        string? statusMessage,
        bool purchasing,
        List<ShipyardVesselEntry> vessels)
    {
        Balance = balance;
        StatusMessage = statusMessage;
        Purchasing = purchasing;
        Vessels = vessels;
    }
}

[Serializable, NetSerializable]
public sealed class ShipyardVesselEntry
{
    public readonly string PrototypeId;
    public readonly string Name;
    public readonly string Description;
    public readonly int Price;
    public readonly bool CanPurchase;
    public readonly string? UnavailableReason;

    public ShipyardVesselEntry(
        string prototypeId,
        string name,
        string description,
        int price,
        bool canPurchase,
        string? unavailableReason)
    {
        PrototypeId = prototypeId;
        Name = name;
        Description = description;
        Price = price;
        CanPurchase = canPurchase;
        UnavailableReason = unavailableReason;
    }
}

/// <summary>
/// Запрос покупки выбранного шаттла.
/// </summary>
[Serializable, NetSerializable]
public sealed class ShipyardConsolePurchaseMessage : BoundUserInterfaceMessage
{
    public readonly string VesselId;

    public ShipyardConsolePurchaseMessage(string vesselId)
    {
        VesselId = vesselId;
    }
}
