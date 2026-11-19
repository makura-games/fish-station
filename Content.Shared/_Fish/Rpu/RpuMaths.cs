using System.Linq;
using System.Globalization;

namespace Content.Shared._Fish.Rpu;

/// <summary>
/// Чистые расчёты системы: комбинация льгот, кривые, парсинг срока автобана.
/// Вынесено отдельно, чтобы это можно было тестировать без запуска сервера.
/// </summary>
public static class RpuMaths
{
    public const float KarmaMin = -100f;
    public const float KarmaMax = 100f;

    /// <summary>
    /// Комбинирует множители как взвешенное геометрическое средние с затуханием весов:
    /// самые сильные льготы получают больший вес, остальные лишь слегка подтягивают результат.
    /// Это не произведение: пять льгот по 0.6 дают около 0.65, а не 0.08.
    /// </summary>
    public static float CombineMultipliers(IReadOnlyList<float> modifiers,
        float weightDecay = 0.6f,
        float floor = 0.15f,
        float cap = 1.5f)
    {
        if (modifiers.Count == 0)
            return 1f;

        // Абсолютный ноль (полное освобождение) не участвует в взвешивании.
        foreach (var modifier in modifiers)
        {
            if (modifier <= 0f)
                return 0f;
        }

        var sorted = modifiers.OrderBy(x => x).ToArray();
        var decay = Math.Clamp(weightDecay, 0.01f, 0.99f);

        double weighted = 0;
        double totalWeight = 0;
        var weight = 1.0;
        foreach (var modifier in sorted)
        {
            weighted += weight * Math.Log(modifier);
            totalWeight += weight;
            weight *= decay;
        }

        if (totalWeight <= 0)
            return 1f;

        var result = (float)Math.Exp(weighted / totalWeight);
        return Math.Clamp(result, floor, cap);
    }

    /// <summary>
    /// Кривая убийств: первые два — ×0.5, дальше ×1.0, с 7-го ×1.2 и +0.05 за каждое, потолок ×1.5.
    /// </summary>
    public static float KillEscalation(int kills, float cap = 1.5f)
    {
        if (kills <= 2)
            return 0.5f;

        if (kills < 7)
            return 1f;

        return Math.Min(cap, 1.2f + (kills - 7) * 0.05f);
    }

    /// <summary>
    /// Порог автобана по наигранным часам и сдвигом от истории банов.
    /// </summary>
    public static float BanThreshold(double roleHours,
        IReadOnlyList<RpuBanTier> tiers,
        float thresholdShift,
        bool banVeterans)
    {
        var fallback = -100f;

        foreach (var tier in tiers.OrderBy(t => t.MaxHours))
        {
            if (roleHours >= tier.MaxHours)
                continue;

            if (tier.VeteransOnly && !banVeterans)
                return float.NegativeInfinity; // не банить

            return tier.Threshold + Math.Abs(thresholdShift);
        }

        // выше всех границ — ветераны
        if (!banVeterans)
            return float.NegativeInfinity;

        return fallback;
    }

    /// <summary>Множитель по истории банов (последний подходящий tier).</summary>
    public static (float Multiplier, float ThresholdShift) BanHistoryModifiers(int bans,
        IReadOnlyList<RpuBanHistoryTier> tiers)
    {
        var multiplier = 1f;
        var shift = 0f;

        foreach (var tier in tiers.OrderBy(t => t.MinBans))
        {
            if (bans < tier.MinBans)
                continue;

            multiplier = tier.Multiplier;
            shift = tier.ThresholdShift;
        }

        return (multiplier, shift);
    }

    /// <summary>
    /// Линейная интерполяция эффекта давления: на startKarma значение минимальное,
    /// на endKarma (обычно -100) — максимальное. Выше startKarma — 0, эффект выключен.
    /// </summary>
    public static float ScaleByKarma(float karma, float startKarma, float endKarma, float min, float max)
    {
        if (karma >= startKarma)
            return 0f;

        if (Math.Abs(endKarma - startKarma) < 0.001f)
            return max;

        var t = Math.Clamp((startKarma - karma) / (startKarma - endKarma), 0f, 1f);
        return min + (max - min) * t;
    }

    public static float ClampKarma(float karma)
    {
        return Math.Clamp(karma, KarmaMin, KarmaMax);
    }
}
