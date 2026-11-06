using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Xenobiology;

/// <summary>Defines a slime lineage and its possible mutations without changing ordinary slimes.</summary>
[Prototype]
public sealed partial class FishXenobiologyBreedPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Fork identifier shown in tooling alongside the lineage.</summary>
    [DataField]
    public string Suffix = "Fish";

    /// <summary>Localized name displayed by laboratory equipment.</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>Localized description of the lineage.</summary>
    [DataField(required: true)]
    public LocId Description;

    /// <summary>Color of the slime and its extract.</summary>
    [DataField]
    public Color Color = Color.White;

    /// <summary>Distance from the starter lineage in the mutation tree.</summary>
    [DataField]
    public int Tier;

    /// <summary>Juvenile entity spawned when this lineage is inherited.</summary>
    [DataField(required: true)]
    public EntProtoId Mob;

    /// <summary>Extract produced by processing an adult corpse.</summary>
    [DataField(required: true)]
    public EntProtoId Extract;

    /// <summary>Weighted mutation candidates, independent of the parent's mutation probability.</summary>
    [DataField]
    public Dictionary<ProtoId<FishXenobiologyBreedPrototype>, float> Mutations = new();

    /// <summary>Well-fed time required to become an adult, before inherited growth modifiers.</summary>
    [DataField]
    public float GrowthDuration = 180f;

    /// <summary>Food biomass consumed on reaching adulthood.</summary>
    [DataField]
    public float GrowthBiomass = 20f;

    /// <summary>Additional biomass required to divide an adult.</summary>
    [DataField]
    public float DivisionBiomass = 40f;

    /// <summary>Lower comfortable body temperature, in kelvin.</summary>
    [DataField]
    public float MinimumTemperature = 283.15f;

    /// <summary>Upper comfortable body temperature, in kelvin.</summary>
    [DataField]
    public float MaximumTemperature = 310.15f;
}

/// <summary>Appearance keys sent by the server to the generic slime visualizer.</summary>
[Serializable, NetSerializable]
public enum FishXenobiologyVisuals : byte
{
    Stage,
}

/// <summary>Sprite layers used by all laboratory slimes.</summary>
[Serializable, NetSerializable]
public enum FishXenobiologyLayers : byte
{
    Body,
}

/// <summary>The physical life stage of a laboratory slime.</summary>
[Serializable, NetSerializable]
public enum FishXenobiologyStage : byte
{
    Baby,
    Adult,
    DeadBaby,
    DeadAdult,
}
