using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Fish.CentCommSupply;

/// <summary>
/// A product that Central Command can order through the supply console.
/// </summary>
[Prototype]
public sealed partial class CentCommSupplyProductPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Localized product name shown in the console.
    /// </summary>
    [DataField(required: true)]
    public LocId Name = string.Empty;

    /// <summary>
    /// Optional localized product description.
    /// </summary>
    [DataField]
    public LocId? Description;

    /// <summary>
    /// Localized category used to filter products in the console.
    /// </summary>
    [DataField]
    public LocId Category = "centcomm-supply-category-general";

    /// <summary>
    /// Icon displayed for the product in the console.
    /// </summary>
    [DataField]
    public SpriteSpecifier? Icon;

    /// <summary>
    /// Entities spawned for one unit of this product.
    /// </summary>
    [DataField(required: true)]
    public List<CentCommSupplyContentEntry> Contents = new();

    /// <summary>
    /// Maximum quantity of this product in a single order.
    /// </summary>
    [DataField]
    public int MaxQuantity = 10;

    /// <summary>
    /// Whether the product is available in the console.
    /// </summary>
    [DataField]
    public bool Enabled = true;

    /// <summary>
    /// Ordering priority in the console list. Lower values are shown first.
    /// </summary>
    [DataField]
    public int SortOrder = 100;
}

[DataDefinition]
public sealed partial class CentCommSupplyContentEntry
{
    [DataField(required: true)]
    public EntProtoId Id = string.Empty;

    [DataField]
    public int Amount = 1;
}
