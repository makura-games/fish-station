using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Research.Bluespace;

/// <summary>One weighted, bounded batch of harvester products in an operator-selectable category.</summary>
[Prototype]
public sealed partial class FishBluespaceHarvesterLootPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Fork metadata identifying this loot table.</summary>
    [DataField]
    public string Suffix = "Fish";

    /// <summary>Operator-selectable category containing this batch.</summary>
    [DataField(required: true)]
    public FishBluespaceLootCategory Category;

    /// <summary>Relative probability among valid batches of the same category.</summary>
    [DataField]
    public float Weight = 1f;

    /// <summary>Entity prototype and unit quantity, split into native stacks; batches allow at most 300 units and sixteen entities.</summary>
    [DataField(required: true)]
    public Dictionary<EntProtoId, int> Products = new();
}
