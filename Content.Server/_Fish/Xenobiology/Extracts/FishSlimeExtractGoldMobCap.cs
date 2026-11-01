namespace Content.Server._Fish.Xenobiology.Extracts;

/// <summary>
/// Общий кап мобов от gold plasma crit (паритет SS220, точное число в оригинале не найдено).
/// </summary>
public static class FishSlimeExtractGoldMobCap
{
    public const int MaxActive = 15;

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
