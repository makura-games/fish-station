using System;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.CentCommSupply;

[Serializable, NetSerializable]
public enum CentCommSupplyUiKey : byte
{
    Key,
}

/// <summary>
/// Current state of the CentComm supply console.
/// </summary>
[Serializable, NetSerializable]
public sealed class CentCommSupplyBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly List<CentCommSupplyProductEntry> Products;
    public readonly Dictionary<NetEntity, string> AvailableStations;
    public readonly NetEntity? SelectedStation;
    public readonly int MaxCratesPerOrder;
    public readonly int MaxProductsPerOrder;
    public readonly Guid? LastStationSelectionId;
    public readonly string? StatusMessage;
    public readonly bool OrderPlaced;

    public CentCommSupplyBoundUserInterfaceState(
        List<CentCommSupplyProductEntry> products,
        Dictionary<NetEntity, string> availableStations,
        NetEntity? selectedStation,
        int maxCratesPerOrder,
        int maxProductsPerOrder,
        Guid? lastStationSelectionId,
        string? statusMessage,
        bool orderPlaced)
    {
        Products = products;
        AvailableStations = availableStations;
        SelectedStation = selectedStation;
        MaxCratesPerOrder = maxCratesPerOrder;
        MaxProductsPerOrder = maxProductsPerOrder;
        LastStationSelectionId = lastStationSelectionId;
        StatusMessage = statusMessage;
        OrderPlaced = orderPlaced;
    }
}

[Serializable, NetSerializable]
public sealed class CentCommSupplyProductEntry
{
    public readonly string PrototypeId;
    public readonly string Name;
    public readonly string? Description;
    public readonly string Category;
    public readonly int MaxQuantity;

    public CentCommSupplyProductEntry(
        string prototypeId,
        string name,
        string? description,
        string category,
        int maxQuantity)
    {
        PrototypeId = prototypeId;
        Name = name;
        Description = description;
        Category = category;
        MaxQuantity = maxQuantity;
    }
}

[Serializable, NetSerializable]
public sealed class CentCommSupplySelectStationMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Station;
    public readonly Guid SelectionId;

    public CentCommSupplySelectStationMessage(NetEntity station, Guid selectionId)
    {
        Station = station;
        SelectionId = selectionId;
    }
}

[Serializable, NetSerializable]
public sealed class CentCommSupplyOrderMessage : BoundUserInterfaceMessage
{
    public readonly Guid OrderId;
    public readonly NetEntity Station;
    public readonly List<CentCommSupplyOrderEntry> Entries;

    public CentCommSupplyOrderMessage(Guid orderId, NetEntity station, List<CentCommSupplyOrderEntry> entries)
    {
        OrderId = orderId;
        Station = station;
        Entries = entries;
    }
}

[Serializable, NetSerializable]
public sealed class CentCommSupplyOrderEntry
{
    public readonly string ProductId;
    public readonly int Quantity;

    public CentCommSupplyOrderEntry(string productId, int quantity)
    {
        ProductId = productId;
        Quantity = quantity;
    }
}
