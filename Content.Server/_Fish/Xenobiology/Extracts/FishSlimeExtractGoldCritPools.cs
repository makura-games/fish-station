using Robust.Shared.Random;

namespace Content.Server._Fish.Xenobiology.Extracts;

/// <summary>
/// Пул мобов gold crit (упрощённый roster SS220: враждебные / нейтральные / дружелюбный).
/// </summary>
public static class FishSlimeExtractGoldCritPools
{
    public static readonly string[] HostilePlasma =
    [
        "MobSpiderAngryBase",
        "MobSpiderSpace",
        "MobBearSpace",
        "MobSpiderSpaceSalvage",
        "MobBearSpaceSalvage",
    ];

    public const string NeutralBlood = "FishXenoGoldCritNeutralCarpFish";
    public const string FriendlyWater = "FishXenoGoldCritFriendlyCarpFish";

    public static string PickHostile(IRobustRandom random)
    {
        return random.Pick(HostilePlasma);
    }
}
