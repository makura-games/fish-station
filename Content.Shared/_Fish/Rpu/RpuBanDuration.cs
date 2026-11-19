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

            duration = TimeSpan.FromMinutes(bare);
            return true;
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

        duration = TimeSpan.FromMinutes(amount * unitMinutes);
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
