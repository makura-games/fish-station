namespace Content.Server._Fish.Research.Bluespace;

/// <summary>Configures an APC-powered research source which raises its station's bluespace background.</summary>
[RegisterComponent, Access(typeof(FishBluespaceResearchSystem))]
public sealed partial class FishBluespaceProberComponent : Component
{
    /// <summary>Requested operating state; physical and research conditions are checked separately.</summary>
    [DataField]
    public bool Enabled;

    /// <summary>Research points generated per active second before background and population modifiers.</summary>
    [DataField]
    public float PointsPerSecond = 5f;

    /// <summary>Station background added per active second.</summary>
    [DataField]
    public float BackgroundPerSecond = 0.04f;

    /// <summary>Divides each prober's output by the active station prober count raised to this power.</summary>
    [DataField]
    public float DiminishingExponent = 0.5f;

    /// <summary>Additional output multiplier at one hundred background.</summary>
    [DataField]
    public float BackgroundPointMultiplier = 1f;

    internal double PointAccumulator;
    internal float AccrualElapsed;
    internal float PointsPerMinute;
    internal float UiElapsed = 1f;
    internal EntityUid? LastServer;
    internal EntityUid? LastScope;
}
