using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Xenobiology;

[Serializable, NetSerializable]
public enum FishXenobiologyUiKey : byte
{
    Key,
}

/// <summary>Read-only measurements from a powered colony console.</summary>
[Serializable, NetSerializable]
public sealed class FishXenobiologyBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool Powered;
    public float Range;
    public List<FishXenobiologySpecimen> Specimens = new();
}

/// <summary>Authoritative input, operating state and extraction progress of a slime processor.</summary>
[Serializable, NetSerializable]
public sealed class FishXenobiologyProcessorBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool Powered;
    public bool HasInput;
    public bool Processing;
    public bool CanProcess;
    public bool CanEject;
    public float Progress;
    public float SecondsRemaining;
    public float Duration;
    public int ExpectedYield;
    public FishXenobiologySpecimen? Input;
    public FishXenobiologyProcessorStatus Status;
}

[Serializable, NetSerializable]
public enum FishXenobiologyProcessorStatus : byte
{
    NoPower,
    Empty,
    Ready,
    Processing,
    InvalidInput,
}

/// <summary>A measured specimen; no client-supplied identity is used for laboratory actions.</summary>
[Serializable, NetSerializable]
public sealed class FishXenobiologySpecimen
{
    public NetEntity Entity;
    public string Name = string.Empty;
    public string Identifier = string.Empty;
    public string Breed = string.Empty;
    public bool Adult;
    public bool Dead;
    public bool Hostile;
    public bool Suspended;
    public int Generation;
    public float Growth;
    public float Biomass;
    public float Hunger;
    public float Temperature;
    public float MinimumTemperature;
    public float MaximumTemperature;
    public float GrowthBiomass;
    public float DivisionBiomass;
    public float MutationChance;
    public int Offspring;
    public int ExtractYield;
    public float GrowthRate;
    public float Docility;
    public float ThermalTolerance;
}

/// <summary>Requests processing of the machine's existing input, with server-side revalidation.</summary>
[Serializable, NetSerializable]
public sealed class FishXenobiologyProcessMessage : BoundUserInterfaceMessage;

/// <summary>Requests safe release of the machine's existing input.</summary>
[Serializable, NetSerializable]
public sealed class FishXenobiologyEjectMessage : BoundUserInterfaceMessage;
