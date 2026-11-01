namespace Content.Shared._Fish.Xenobiology.Organs;

[RegisterComponent]
public sealed partial class FishXenoOrganAnalyzerScanComponent : Component
{
    public EntityUid Organ;
    public TimeSpan FinishAt;
}
