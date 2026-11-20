using System.Globalization;

namespace Content.Shared._Fish.Rpu;

/// <summary>
/// Разбор срока автобана из cvar rpu.ban_duration.
/// </summary>
public static class RpuBanDuration
{
    public const string Help =
        "0 = навсегда; иначе \"<число> <единица>\", единицы: minute/min/мин, hour/h/час, day/d/день, " +
        "week/w/неделя, month/месяц, year/y/год (можно во множественном числе и по-русски). " +
        "Примеры: \"0\", \"6 hours\", \"3 дня\", \"1 year\".";

    /// <summary>
    /// duration == null означает «навсегда».
    /// </summary>
    public static bool TryParse(string? value, out TimeSpan? duration, out string? error)
    {
        duration = null;
        error = null;

        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return true;

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 1)
        {
            // Голое число трактуем как минуты — так привычнее админу после команды ban.
            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var bare))
            {
                error = $"Не удалось разобрать число: {parts[0]}. {Help}";
                return false;
            }

            if (bare <= 0)
                return true;

            return TryFromMinutes(bare, out duration, out error);
        }

        if (parts.Length != 2)
        {
            error = $"Нужно «<число> <единица>» или 0. {Help}";
            return false;
        }

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
        {
            error = $"Не удалось разобрать число: {parts[0]}. {Help}";
            return false;
        }

        if (amount <= 0)
            return true;

        if (!TryUnit(parts[1], out var unitMinutes))
        {
            error = $"Неизвестная единица времени: {parts[1]}. {Help}";
            return false;
        }

        return TryFromMinutes(amount * unitMinutes, out duration, out error);
    }

    /// <summary>
    /// TimeSpan.FromMinutes бросает исключение на переполнении, а нам нужен отказ с текстом:
    /// срок вроде «99999999 years» — это ошибка конфига, а не вечный бан.
    /// </summary>
    private static bool TryFromMinutes(double minutes, out TimeSpan? duration, out string? error)
    {
        duration = null;
        error = null;

        if (!double.IsFinite(minutes))
        {
            error = $"Срок бана не число: {minutes}. {Help}";
            return false;
        }

        if (minutes <= 0)
            return true;

        if (minutes > TimeSpan.MaxValue.TotalMinutes)
        {
            error = $"Срок бана слишком большой: {minutes} минут. {Help}";
            return false;
        }

        duration = TimeSpan.FromMinutes(minutes);
        return true;
    }

    private static bool TryUnit(string unit, out double minutes)
    {
        switch (unit.ToLowerInvariant().Trim('.', ' '))
        {
            case "minute":
            case "minutes":
            case "min":
            case "mins":
            case "мин":
            case "минута":
            case "минуты":
            case "минут":
                minutes = 1;
                return true;
            case "hour":
            case "hours":
            case "h":
            case "час":
            case "часа":
            case "часов":
                minutes = 60;
                return true;
            case "day":
            case "days":
            case "d":
            case "день":
            case "дня":
            case "дней":
            case "сутки":
                minutes = 60 * 24;
                return true;
            case "week":
            case "weeks":
            case "w":
            case "неделя":
            case "недели":
            case "недель":
                minutes = 60 * 24 * 7;
                return true;
            case "month":
            case "months":
            case "месяц":
            case "месяца":
            case "месяцев":
                minutes = 60 * 24 * 30;
                return true;
            case "year":
            case "years":
            case "y":
            case "год":
            case "года":
            case "годов":
            case "лет":
                minutes = 60 * 24 * 365;
                return true;
        }

        minutes = 0;
        return false;
    }
}
