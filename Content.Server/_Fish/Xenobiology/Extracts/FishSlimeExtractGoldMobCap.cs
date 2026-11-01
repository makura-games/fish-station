namespace Content.Server._Fish.Xenobiology.Extracts;

/// <summary>
/// Общий кап мобов от gold extract (Paradise MAX_GOLD_CORE_MOBS = 45).
/// </summary>
public static class FishSlimeExtractGoldMobCap
{
    public const int MaxActive = FishSlimeExtractGoldCritPools.MaxActive;

    public static int ActiveCount;

    public static bool TryReserve(int count)
    {
        if (ActiveCount + count > MaxActive)
            return false;

        ActiveCount += count;
        return true;
    }

    public static void Release(int count)
    {
        ActiveCount = Math.Max(0, ActiveCount - count);
    }
}
