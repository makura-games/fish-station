using Content.Shared._Fish.Xenobiology;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Xenobiology;

/// <summary>A reusable industrial core, distinct from the ordinary single-use extract.</summary>
[RegisterComponent]
public sealed partial class FishXenobiologyIndustrialComponent : Component
{
    /// <summary>Induction lineage, fuel cost, cycle duration and outputs.</summary>
    [DataField(required: true)]
    public ProtoId<FishXenobiologyIndustrialRecipePrototype> Recipe;

    /// <summary>Refillable solution accepting liquid plasma.</summary>
    [DataField]
    public string Solution = "extract";

    /// <summary>Prevents chemistry callbacks from starting a nested batch.</summary>
    public bool Producing;
}

/// <summary>Marks fueled industrial extracts that need placement checks and cycle updates.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class FishXenobiologyIndustrialActiveComponent : Component
{
    /// <summary>The next batch deadline, cleared whenever production is interrupted.</summary>
    [AutoPausedField]
    public TimeSpan? NextProduction;
}

/// <summary>Tracks ordinary extracts fed to an induced adult and blocks competing lifecycle actions.</summary>
[RegisterComponent]
public sealed partial class FishXenobiologyIndustrialInductionComponent : Component
{
    /// <summary>The recipe selected and fixed when the catalyst was consumed.</summary>
    public ProtoId<FishXenobiologyIndustrialRecipePrototype> Recipe;

    /// <summary>Number of unused same-lineage ordinary extracts claimed so far.</summary>
    public int ExtractsFed;

    /// <summary>Claims the final transformation before spawning or deleting entities.</summary>
    public bool Consumed;
}
