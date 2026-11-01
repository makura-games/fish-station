using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Xenobiology;

/// <summary>Heritable traits and lifecycle state of a laboratory slime.</summary>
[RegisterComponent]
public sealed partial class FishXenobiologySlimeComponent : Component
{
    /// <summary>Lineage governing mutation choices, maturation and extraction.</summary>
    [DataField]
    public ProtoId<FishXenobiologyBreedPrototype> Breed = "FishGrey";

    /// <summary>Whether this specimen has reached adulthood.</summary>
    [DataField]
    public bool Adult;

    /// <summary>Accumulated growth time while fed and at a suitable temperature.</summary>
    [ViewVariables]
    public float Growth;

    /// <summary>Food actually swallowed, available for maturation or division.</summary>
    [DataField]
    public float Biomass;

    /// <summary>Number of divisions separating this specimen from a starter culture.</summary>
    [DataField]
    public int Generation;

    /// <summary>Inherited chance of a child changing lineage, before diet and temperature modifiers.</summary>
    [DataField]
    public float MutationChance = 0.25f;

    /// <summary>Number of juveniles created by a division.</summary>
    [DataField]
    public int Offspring = 2;

    /// <summary>Number of extracts obtained from this adult's corpse.</summary>
    [DataField]
    public int ExtractYield = 1;

    /// <summary>Inherited multiplier for maturation speed.</summary>
    [DataField]
    public float GrowthRate = 1f;

    /// <summary>Inherited resistance to starvation aggression; at one the specimen remains tame.</summary>
    [DataField]
    public float Docility;

    /// <summary>Inherited widening of the comfortable temperature range, in kelvin.</summary>
    [DataField]
    public float ThermalTolerance;

    /// <summary>One-time guard shared by division and corpse extraction.</summary>
    [ViewVariables]
    public bool Consumed;

    /// <summary>Whether development and NPC behavior are suspended inside a container.</summary>
    [ViewVariables]
    public bool Suspended;

    /// <summary>Whether starvation has switched the specimen to the hungry faction.</summary>
    [ViewVariables]
    public bool Hostile;

    /// <summary>Remaining time in which a recently swallowed meal suppresses starvation aggression.</summary>
    [ViewVariables]
    public float FeedingGrace;

    /// <summary>Protein swallowed during this specimen's lifetime.</summary>
    [ViewVariables]
    public float ProteinIntake;

    /// <summary>Plant nutrient swallowed during this specimen's lifetime.</summary>
    [ViewVariables]
    public float NutrimentIntake;

    /// <summary>Time exposed to cool conditions, used to bias mutation choices.</summary>
    [ViewVariables]
    public float ColdExposure;

    /// <summary>Time exposed to warm conditions, used to bias mutation choices.</summary>
    [ViewVariables]
    public float WarmExposure;

    /// <summary>Whether containment temporarily stopped an enabled HTN.</summary>
    [ViewVariables]
    public bool ResumeHtn;
}

/// <summary>Completes manual division after the handler remains next to the specimen.</summary>
[Serializable, NetSerializable]
public sealed partial class FishXenobiologyDivideDoAfterEvent : SimpleDoAfterEvent;
