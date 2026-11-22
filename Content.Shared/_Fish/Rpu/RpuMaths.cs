using System.Linq;

namespace Content.Shared._Fish.Rpu;

/// <summary>
/// Расчёт кармы в очках. Никаких свёрнутых формул и средних: за нарушение снимаем
/// столько-то очков, сверху прибавляем очки за отягчающие, снизу вычитаем очки за смягчающие.
/// Единственные ограничения — минимальное снятие и потолок надбавки.
/// </summary>
public static class RpuMaths
{
    /// <summary>Нижняя рамка кармы.</summary>
    public const float KarmaMin = -100f;

    /// <summary>Верхняя рамка кармы.</summary>
    public const float KarmaMax = 100f;

    /// <summary>
    /// Минимальное снятие за нарушение, которое вообще вменяется: десятая часть очка.
    /// Льготы не должны превращать вменяемый проступок в ноль — для этого есть waives.
    /// </summary>
    public const float MinPenalty = 0.1f;

    /// <summary>Сколько очков можно добавить к базовой цене нарушения сверху, не больше.</summary>
    public const float MaxExtraPenalty = 6f;

    /// <summary>
    /// Итоговое снятие очков: база + тяжесть случая + все поправки (со знаком).
    /// Ограничено снизу <see cref="MinPenalty"/> (одна десятая очка), сверху — базой плюс
    /// <see cref="MaxExtraPenalty"/>.
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

        // Потолок не может оказаться ниже рамки: Math.Clamp на перевёрнутых границах бросает
        // исключение, а кривой конфиг не должен ронять обработку нарушения.
        var ceiling = Math.Max(minPenalty, baseUnits + Math.Max(0f, maxExtra));

        return Math.Clamp(baseUnits + sum, minPenalty, ceiling);
    }

    /// <summary>
    /// Отягчающие очки за серию убийств в раунде. Первые два — дешевле (первое случайное
    /// попадание или недосмотр не должны стоить полной цены), дальше цена растёт ступенями:
    /// серия из нескольких трупов стоит заметно дороже одиночного убийства.
    /// Потолок надбавки к базе всё равно задаёт <see cref="Penalty"/>.
    /// </summary>
    public static float KillUnits(int kills)
    {
        if (kills <= 0)
            return 0f;

        return kills switch
        {
            1 => -3f,
            2 => -1f,
            3 => 1f,
            4 => 3f,
            <= 6 => 5f,
            _ => Math.Min(12f, 6f + (kills - 6) * 2f),
        };
    }

    /// <summary>
    /// Сколько очков кармы игрок потерял за последние <paramref name="window"/>.
    /// Список потерь упорядочен по времени, поэтому идём с конца и останавливаемся
    /// на первом устаревшем элементе.
    /// </summary>
    public static float LossInWindow(IReadOnlyList<RpuLoss> losses, TimeSpan now, TimeSpan window)
    {
        if (losses.Count == 0)
            return 0f;

        var sum = 0f;

        for (var i = losses.Count - 1; i >= 0; i--)
        {
            var loss = losses[i];
            if (now - loss.At > window)
                break;

            sum += loss.Amount;
        }

        return sum;
    }

    /// <summary>Одна учтённая потеря кармы.</summary>
    public readonly record struct RpuLoss(TimeSpan At, float Amount);

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

        // Сдвиг от истории банов действует и на последней ступени тоже.
        return fallback + Math.Abs(thresholdShift);
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
