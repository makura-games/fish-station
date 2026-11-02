namespace Content.Server._Fish.Xenobiology.Extracts;

/// <summary>
/// Константы gold crit; legacy-пулы — fallback для <see cref="FishSlimeExtractGoldCritPoolSystem"/>.
/// </summary>
public static class FishSlimeExtractGoldCritPools
{
    /// <summary>Paradise MAX_GOLD_CORE_MOBS.</summary>
    public const int MaxActive = 45;

    /// <summary>Paradise addtimer 50 deciseconds.</summary>
    public static readonly TimeSpan SpawnDelay = TimeSpan.FromSeconds(5);

    /// <summary>Fallback, если ни один прототип не помечен spawnable.</summary>
    internal static readonly string[] LegacyHostilePlasma =
    [
        "MobSpiderAngryBase",
        "MobSpiderSpace",
        "MobSpiderSpaceSalvage",
        "MobBearSpace",
        "MobBearSpaceSalvage",
        "MobHivebot",
        "MobGoliath",
        "MobHivelord",
        "MobWatcherLavaland",
        "MobWatcherIcewing",
        "MobWatcherMagmawing",
        "MobHellspawn",
        "MobFleshGolem",
        "MobAbomination",
        "MobCarp",
    ];

    internal static readonly string[] LegacyLesserBlood =
    [
        "FishXenoGoldCritNeutralCarpFish",
        "FishXenoGoldCritLesserMagicarpFish",
        "FishXenoGoldCritLesserHolocarpFish",
        "MobCarpMagic",
        "MobCarpHolo",
    ];

    internal static readonly string[] LegacyFriendlyWater =
    [
        "FishXenoGoldCritFriendlyCarpFish",
        "FishXenoGoldCritFriendlyCatFish",
        "FishXenoGoldCritFriendlyPenguinFish",
    ];
}
