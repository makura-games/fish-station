using System.Linq;

namespace Content.Shared._Fish.Rpu;

/// <summary>
/// Расчёт кармы в очках. Никаких свёрнутых формул и средних: за нарушение снимаем
/// столько-то очков, сверху прибавляем очки за отягчающие, снизу вычитаем очки за смягчающие.
/// Единственные ограничения — минимальное снятие и потолок надбавки.
/// </summary>
/// <remarks>
/// Нормативные правила (совместно с <c>RpuSystem.Penalize</c>):
/// <list type="number">
/// <item>Берём базовую цену нарушения в очках кармы (прототип rpuViolation, поле base).</item>
/// <item>Прибавляем очки за тяжесть конкретного случая (сколько разлил реактива,
/// насколько большой получился взрыв и так далее).</item>
/// <item>Прибавляем очки за отягчающие (новичок, серия убийств, прежние баны) и вычитаем очки
/// за смягчающие (роль, отдел, обстановка, код тревоги). Каждая поправка — целое число очков.</item>
/// <item>Снятие не может быть меньше <see cref="MinPenalty"/> очка и больше базы на
/// <see cref="MaxExtraPenalty"/> очков.</item>
/// <item>Карма после вычитания ограничена диапазонами <see cref="KarmaMin"/>..<see cref="KarmaMax"/>.</item>
/// <item>Если категория нарушения игроку вообще не вменяется (освобождение), не снимается ничего.</item>
/// </list>
/// </remarks>
public static class RpuMaths
{
    /// <summary>Нижняя рамка кармы.</summary>
    public const float KarmaMin = -100f;

    /// <summary>Верхняя рамка кармы.</summary>
    public const float KarmaMax = 100f;

    /// <summary>Минимальное снятие за нарушение, которое вообще вменяется.</summary>
    public const float MinPenalty = 1f;

    /// <summary>Сколько очков можно добавить к базовой цене нарушения сверху, не больше.</summary>
    public const float MaxExtraPenalty = 6f;

    /// <summary>
    /// Итоговое снятие очков: база + тяжесть случая + все поправки (со знаком).
    /// Ограничено снизу одним очком, сверху — базой плюс <see cref="MaxExtraPenalty"/>.
    /// </summary>
    public static float Penalty(float baseUnits,
        IReadOnlyList<float> adjustments,
        float extra = 0f,
        float minPenalty = MinPenalty,
        float maxExtra = MaxExtraPenalty)
    {
        var sum = extra;
        foreach (var unit in adjustments)
            sum += unit;

        return Math.Clamp(baseUnits + sum, minPenalty, baseUnits + Math.Max(0f, maxExtra));
    }

    /// <summary>
    /// Отягчающие очки за серию убийств в раунде: первые два — на 2 очка дешевле,
    /// с седьмого — плюс очко за каждые четыре следующих, потолок +4 очка.
    /// </summary>
    public static float KillUnits(int kills)
    {
        if (kills <= 2)
            return -2f;

        if (kills < 7)
            return 0f;

        return Math.Min(4f, 1f + (kills - 7) / 4);
    }

    /// <summary>
    /// Очки по истории банов и сдвиг порога автобана (в очках кармы).
    /// Берётся последний подходящий интервал, складывать их нельзя.
    /// </summary>
    public static (float Units, float ThresholdShift) BanHistory(int bans, IReadOnlyList<RpuBanHistoryTier> tiers)
    {
        var units = 0f;
        var shift = 0f;

        foreach (var tier in tiers.OrderBy(t => t.MinBans))
        {
            if (bans < tier.MinBans)
                continue;

            units = tier.Units;
            shift = tier.ThresholdShift;
        }

        return (units, shift);
    }

    /// <summary>
    /// Порог автобана по наигранным часам и сдвигу от истории банов.
    /// Значение float.NegativeInfinity означает «не банить».
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
                return float.NegativeInfinity;

            return tier.Threshold + Math.Abs(thresholdShift);
        }

        if (!banVeterans)
            return float.NegativeInfinity;

        return fallback;
    }

    /// <summary>
    /// Линейная интерполяция эффекта давления: на startKarma значение минимальное,
    /// на endKarma (обычно -100) — максимальное. Выше startKarma эффект выключен.
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

    /// <summary>Рамки кармы: -100 .. +100.</summary>
    public static float ClampKarma(float karma)
    {
        return Math.Clamp(karma, KarmaMin, KarmaMax);
    }
}
