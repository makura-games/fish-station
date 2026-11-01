using Robust.Shared.Random;

namespace Content.Server._Fish.Xenobiology.Extracts;

/// <summary>
/// Пулы gold crit — соответствие Paradise HOSTILE_SPAWN / lesser / FRIENDLY_SPAWN на прототипы SS14.
/// </summary>
public static class FishSlimeExtractGoldCritPools
{
    /// <summary>Paradise MAX_GOLD_CORE_MOBS.</summary>
    public const int MaxActive = 45;

    /// <summary>Paradise addtimer 50 deciseconds.</summary>
    public static readonly TimeSpan SpawnDelay = TimeSpan.FromSeconds(5);

    /// <summary>Plasma — «Gold Slime», 5× HOSTILE_SPAWN.</summary>
    public static readonly string[] HostilePlasma =
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

    /// <summary>Blood — «Lesser Gold Slime», 3× (не агрессивны сразу).</summary>
    public static readonly string[] LesserBlood =
    [
        "FishXenoGoldCritNeutralCarpFish",
        "FishXenoGoldCritLesserMagicarpFish",
        "FishXenoGoldCritLesserHolocarpFish",
        "MobCarpMagic",
        "MobCarpHolo",
    ];

    /// <summary>Water — «Friendly Gold Slime», 1× FRIENDLY_SPAWN.</summary>
    public static readonly string[] FriendlyWater =
    [
        "FishXenoGoldCritFriendlyCarpFish",
        "FishXenoGoldCritFriendlyCatFish",
        "FishXenoGoldCritFriendlyPenguinFish",
    ];

    public static string PickHostile(IRobustRandom random) => random.Pick(HostilePlasma);

    public static string PickLesser(IRobustRandom random) => random.Pick(LesserBlood);

    public static string PickFriendly(IRobustRandom random) => random.Pick(FriendlyWater);
}
