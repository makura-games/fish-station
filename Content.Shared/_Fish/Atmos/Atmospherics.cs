namespace Content.Shared.Atmos;

public static partial class Atmospherics
{
    /// <summary>
    ///     Максимальная доля молей газовой смеси, которую одна реакция вправе превратить
    ///     за один тик (10%).
    /// </summary>
    /// <remarks>
    ///     Без этого ограничения реакции синтеза/разложения выедали до 100% лимитирующего
    ///     реагента за тик, и газ превращался целиком сразу после достижения нужных условий,
    ///     хотя должен расходоваться постепенно.
    /// </remarks>
    public const float MaxConversionFractionPerTick = 0.1f;

    /// <summary>
    ///     Температура, до которой тепловыделение горения плазмы и трития остаётся
    ///     полностью ванильным.
    /// </summary>
    public const float FireHeatReductionStartTemperature = 3000f;

    /// <summary>
    ///     Температура, при которой горение плазмы и трития перестаёт греть — мягкий
    ///     потолок. Нужен, чтобы замкнутые объёмы вроде сабфрейма не разгонялись
    ///     за 100 000 K.
    /// </summary>
    public const float FireHeatCapTemperature = 10000f;

    /// <summary>
    ///     Множитель тепловыделения горения в зависимости от текущей температуры.
    ///     До <see cref="FireHeatReductionStartTemperature"/> равен 1 (ванильное поведение),
    ///     выше — линейно падает до 0 при <see cref="FireHeatCapTemperature"/>. Так обычные
    ///     пожары не слабеют, а разгон в замкнутом объёме давится.
    /// </summary>
    public static float GetFireEnergyReleasedMultiplier(float temperature)
    {
        if (temperature <= FireHeatReductionStartTemperature)
            return 1f;

        if (temperature >= FireHeatCapTemperature)
            return 0f;

        return 1f - (temperature - FireHeatReductionStartTemperature)
                    / (FireHeatCapTemperature - FireHeatReductionStartTemperature);
    }
}
