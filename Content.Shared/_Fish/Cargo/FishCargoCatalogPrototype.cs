using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Cargo;

/// <summary>Assigns cargo products to departmental request consoles without changing products or prices.</summary>
[Prototype]
public sealed partial class FishCargoCatalogPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Fork metadata identifying this catalog.</summary>
    [DataField]
    public string Suffix = "Fish";

    /// <summary>Account used for general supplies and categories without an explicit department.</summary>
    [DataField]
    public ProtoId<CargoAccountPrototype> DefaultAccount = "Cargo";

    /// <summary>Departmental account assigned to each cargo category localization key.</summary>
    [DataField]
    public Dictionary<string, ProtoId<CargoAccountPrototype>> Categories = new();

    /// <summary>Product-specific assignments overriding category defaults, such as first-aid kits.</summary>
    [DataField]
    public Dictionary<ProtoId<CargoProductPrototype>, ProtoId<CargoAccountPrototype>> Products = new();
}
