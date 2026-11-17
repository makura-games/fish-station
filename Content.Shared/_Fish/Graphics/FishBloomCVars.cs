using Robust.Shared.Configuration;

namespace Content.Shared._Fish.Graphics;

/// <summary>
/// Client settings for bloom around emissive sprites and tiles.
/// </summary>
[CVarDefs]
public static class FishBloomCVars
{
    public const string QualityLow = "Low";
    public const string QualityMedium = "Medium";
    public const string QualityHigh = "High";

    /// <summary>Enables the client bloom overlay.</summary>
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("fish.bloom_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Bloom intensity in the range from zero to one. Zero disables rendering.</summary>
    public static readonly CVarDef<float> Strength =
        CVarDef.Create("fish.bloom_strength", 0.4f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Resolution profile: Low, Medium or High, without changing the world-space halo radius.</summary>
    public static readonly CVarDef<string> Quality =
        CVarDef.Create("fish.bloom_quality", QualityMedium, CVar.CLIENTONLY | CVar.ARCHIVE);
}
