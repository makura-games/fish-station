namespace Content.Server._Fish.Xenobiology;

/// <summary>Holds a single adult slime corpse for powered, one-time extraction.</summary>
[RegisterComponent]
public sealed partial class FishXenobiologyProcessorComponent : Component
{
    /// <summary>Container used for the corpse.</summary>
    [DataField]
    public string Container = "xeno_input";

    /// <summary>Powered processing time in seconds.</summary>
    [DataField]
    public float Duration = 5f;
}

/// <summary>Only processing machines receive periodic work updates.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class FishXenobiologyProcessingComponent : Component
{
    /// <summary>Start of the current batch; follows map pauses.</summary>
    [AutoPausedField]
    public TimeSpan StartedAt;

    /// <summary>Completion time of the current powered batch; follows map pauses.</summary>
    [AutoPausedField]
    public TimeSpan FinishAt;
}
