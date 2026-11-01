using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Xenobiology;

/// <summary>A single-use extract recipe exposed by the laboratory's reaction catalog.</summary>
[Prototype]
public sealed partial class FishXenobiologyReactionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>The breed that produces this reaction.</summary>
    [DataField(required: true)]
    public ProtoId<FishXenobiologyBreedPrototype> Breed;

    /// <summary>The localized product title.</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>The localized result and handling instructions.</summary>
    [DataField(required: true)]
    public LocId Description;

    /// <summary>The editor suffix identifying Fish content.</summary>
    [DataField]
    public string Suffix = "Fish";

    /// <summary>The chemical consumed to activate this reaction.</summary>
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    /// <summary>The required catalyst quantity; output never scales with excess catalyst.</summary>
    [DataField]
    public FixedPoint2 Amount = FixedPoint2.New(5);

    /// <summary>The finite number of each item produced.</summary>
    [DataField]
    public Dictionary<EntProtoId, int> Products = new();

    /// <summary>The reagents delivered in a separate sample bottle.</summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Reagents = new();
}
