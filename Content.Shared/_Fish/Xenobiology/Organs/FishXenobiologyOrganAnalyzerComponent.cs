namespace Content.Shared._Fish.Xenobiology.Organs;

using Robust.Shared.Prototypes;

[RegisterComponent]
public sealed partial class FishXenobiologyOrganAnalyzerComponent : Component
{
    [DataField]
    public TimeSpan ScanDuration = TimeSpan.FromSeconds(15);

    [DataField]
    public int BasePoints = 100;

    /// <summary>
    /// Не больше стольких успешных сканов одного прототипа органа (SS220: 3).
    /// </summary>
    [DataField]
    public int MaxScansPerOrganType = 3;

    [DataField]
    public Dictionary<string, int> ScanCountsByPrototype = new();

    /// <summary>
    /// Шанс выпадения technology disk после успешного скана (SS220: редко).
    /// </summary>
    [DataField]
    public float TechDiskChance = 0.12f;

    [DataField]
    public EntProtoId TechDiskPrototype = "TechnologyDisk";
}
