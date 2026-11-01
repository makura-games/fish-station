namespace Content.Shared._Fish.Xenobiology.Organs;

[RegisterComponent]
public sealed partial class FishXenobiologyOrganAnalyzerComponent : Component
{
    [DataField]
    public TimeSpan ScanDuration = TimeSpan.FromSeconds(15);

    [DataField]
    public int BasePoints = 100;
}
