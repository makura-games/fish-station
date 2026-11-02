namespace Content.Server._Fish.Research.Bluespace;

/// <summary>Keeps incursion limits on the owning station rather than on individual machines.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class FishBluespaceHarvesterStationComponent : Component
{
    /// <summary>Earliest time at which any harvester on this station may open another rift.</summary>
    [AutoPausedField]
    public TimeSpan NextIncident;

    /// <summary>Creatures which still occupy the station's bounded incursion budget.</summary>
    public HashSet<EntityUid> Incursions = new();

    /// <summary>Earliest time at which a harvester may create another native anomaly.</summary>
    [AutoPausedField]
    public TimeSpan NextAnomaly;

    /// <summary>Active harvester anomalies retained until they are neutralized or deleted.</summary>
    public HashSet<EntityUid> Anomalies = new();

    /// <summary>Reserves the station budget during nested entity spawn callbacks.</summary>
    public bool Spawning;
}
