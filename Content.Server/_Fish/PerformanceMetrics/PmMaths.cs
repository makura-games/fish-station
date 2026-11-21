namespace Content.Server._Fish.PerformanceMetrics;

/// <summary>
/// Чистая арифметика наблюдения: никаких обращений к движку, чтобы это можно было проверить тестом.
/// </summary>
public static class PmMaths
{
    /// <summary>
    /// Насколько направление на жертву расходится с тем, куда смотрел атакующий, в градусах.
    /// Ноль — бил ровно туда, куда повёрнут.
    /// </summary>
    public static float AngularErrorDegrees(double facingRadians, double angleToTargetRadians)
    {
        var diff = facingRadians - angleToTargetRadians;

        // Приводим к [-pi, pi], иначе разворот на 350 градусов посчитается маленьким.
        const double tau = Math.PI * 2.0;
        diff %= tau;
        if (diff > Math.PI)
            diff -= tau;
        if (diff < -Math.PI)
            diff += tau;

        return (float)Math.Abs(diff) * (180f / MathF.PI);
    }

    /// <summary>
    /// Коэффициент вариации межударных интервалов: разброс, делённый на среднее.
    /// У человека он далеко не нулевой; у цикла из кода стремится к нулю.
    /// Возвращает float.PositiveInfinity, когда считать не по чему.
    /// </summary>
    public static float VariationCoefficient(ReadOnlySpan<float> gaps)
    {
        if (gaps.Length < 4)
            return float.PositiveInfinity;

        var mean = 0f;
        foreach (var gap in gaps)
            mean += gap;

        mean /= gaps.Length;
        if (mean <= 0f)
            return float.PositiveInfinity;

        var variance = 0f;
        foreach (var gap in gaps)
        {
            var d = gap - mean;
            variance += d * d;
        }

        variance /= gaps.Length;
        return MathF.Sqrt(variance) / mean;
    }

    /// <summary>
    /// Превышение пройденного пути над разрешённым, в тайлах. Отрицательное значение — в норме.
    /// </summary>
    public static float SpeedExcessTiles(float movedTiles, float allowedSpeed, float seconds, float margin)
    {
        if (seconds <= 0f)
            return 0f;

        return movedTiles - allowedSpeed * margin * seconds;
    }

    /// <summary>
    /// Доля попыток, закончившихся попаданием, среди последних <paramref name="count"/>.
    /// Возвращает -1, когда выборка слишком мала и доля ничего не значит.
    /// </summary>
    public static float RealisedShare(IReadOnlyList<PmAttempt> attempts, int windowSeconds, int count)
    {
        if (attempts.Count < count)
            return -1f;

        var newest = attempts[^1].At;
        var realised = 0;
        var considered = 0;

        for (var i = attempts.Count - count; i < attempts.Count; i++)
        {
            // Совсем старые попытки не в счёт: они относятся к другой схватке.
            if ((newest - attempts[i].At).TotalSeconds > windowSeconds)
                continue;

            considered++;
            if (attempts[i].Realised)
                realised++;
        }

        return considered < count ? -1f : (float) realised / considered;
    }

    /// <summary>
    /// Среди реализованных попыток — доля ударов с угловой ошибкой не больше порога.
    /// -1, когда считать не по чему.
    /// </summary>
    public static float PerfectShareOfRealised(IReadOnlyList<PmAttempt> attempts, int count, float maxErrorDegrees)
    {
        if (attempts.Count < count)
            return -1f;

        var realised = 0;
        var perfect = 0;

        for (var i = attempts.Count - count; i < attempts.Count; i++)
        {
            var attempt = attempts[i];
            if (!attempt.Realised || float.IsNaN(attempt.ErrorDegrees))
                continue;

            realised++;
            if (attempt.ErrorDegrees <= maxErrorDegrees)
                perfect++;
        }

        return realised == 0 ? -1f : (float) perfect / realised;
    }

    /// <summary>Сколько действий в секунду вышло за окно.</summary>
    public static float ActionsPerSecond(int actions, float windowSeconds)
    {
        if (windowSeconds <= 0f)
            return 0f;

        return actions / windowSeconds;
    }

    /// <summary>
    /// Сила признака: сколько независимых подтверждений накопилось и насколько они свежи.
    /// Нужна, чтобы один шумный метрический всплеск не превращался в наказание.
    /// </summary>
    public static int Score(int motionStrikes, int perfectHits, int flickStrikes, int reachStrikes, int actionStrikes)
    {
        var score = 0;

        if (motionStrikes >= 1) score++;
        if (perfectHits >= 1) score++;
        if (flickStrikes >= 1) score++;
        if (reachStrikes >= 1) score++;
        if (actionStrikes >= 1) score++;

        return score;
    }
}
