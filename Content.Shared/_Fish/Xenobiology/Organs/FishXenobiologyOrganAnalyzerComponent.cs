namespace Content.Shared._Fish.Xenobiology.Organs;

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
}
