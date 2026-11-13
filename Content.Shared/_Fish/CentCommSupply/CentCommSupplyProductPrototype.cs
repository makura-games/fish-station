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
    /// Переопределение названия первого предмета в составе товара.
    /// </summary>
    [DataField]
    public LocId? Name;

    /// <summary>
    /// Переопределение описания первого предмета в составе товара.
    /// </summary>
    [DataField]
    public LocId? Description;

    /// <summary>
    /// Localized category used to filter products in the console.
    /// </summary>
    [DataField]
    public LocId Category = "centcomm-supply-category-other";

    /// <summary>
    /// Переопределение иконки первого предмета в составе товара.
    /// </summary>
    [DataField]
    public SpriteSpecifier? Icon;

    /// <summary>
    /// Использовать предпросмотр сущности для спрайтов, которые меняются визуализаторами.
    /// </summary>
    [DataField]
    public bool UseEntityPreview;

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
    /// Fork suffix used to mark Fish supply products.
    /// </summary>
    [DataField]
    public string Suffix = "Fish";

    /// <summary>
    /// Ordering priority in the console list. Lower values are shown first.
    /// </summary>
    [DataField]
    public int SortOrder = 100;

    public string GetName(EntityPrototype entity)
    {
        return Name is { } name ? Loc.GetString(name) : entity.Name;
    }

    public string GetDescription(EntityPrototype entity)
    {
        return Description is { } description ? Loc.GetString(description) : entity.Description;
    }
}

[DataDefinition]
public sealed partial class CentCommSupplyContentEntry
{
    [DataField(required: true)]
    public EntProtoId Id = string.Empty;

    [DataField]
    public int Amount = 1;
}
