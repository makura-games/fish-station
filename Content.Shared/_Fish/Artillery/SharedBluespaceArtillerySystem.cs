namespace Content.Shared._Fish.Artillery;

public abstract partial class SharedBluespaceArtillerySystem : EntitySystem
{
    public const string DefaultExplosionType = "DefaultStandardEffect";
    public const float MinTotalIntensity = 1f;
    public const float MaxTotalIntensity = 1000f;
    public const float MinSlope = 0.1f;
    public const float MaxSlope = 30f;
    public const float MinMaxIntensity = 1f;
    public const float MaxMaxIntensity = 250f;
}
