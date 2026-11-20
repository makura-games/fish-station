namespace Content.Server._Fish.PerformanceMetrics;

/// <summary>
/// Настройки наблюдения за нагрузкой и поведением сессий.
///
/// Держимся в серверной сборке: пороги и признаки не уезжают клиенту и не лежат в YAML,
/// поэтому разобрать их «по ту сторону» нельзя.
/// </summary>
public static class PmTuning
{
    // ---------- движение ----------

    /// <summary>Через сколько тиков сверяем пройденный путь с разрешённой скоростью.</summary>
    public const int MotionSampleTicks = 10;

    /// <summary>Запас против заявленной скорости: рывки после толчков, лаги, телепорты сетки.</summary>
    public const float SpeedMargin = 3.0f;

    /// <summary>Сколько подряд выборок должны превышать норму, прежде чем считать аномалию.</summary>
    public const int MotionStrikesBeforeFlag = 8;

    /// <summary>Минимальный «лишний» путь в тайлах за выборку, который вообще интересен.</summary>
    public const float MotionMinExcessTiles = 2.5f;

    // ---------- прицел и ритм ----------

    /// <summary>Угловая ошибка меньше этой величины при каждом ударе — подозрительно.</summary>
    public const float AimPerfectDegrees = 1.5f;

    /// <summary>Сколько ударов подряд почти нулевая ошибка, прежде чем считать.</summary>
    public const int AimStrikeCount = 12;

    /// <summary>Коэффициент вариации межударных интервалов ниже этого — машинный ритм.</summary>
    public const float CadenceRegularity = 0.08f;

    /// <summary>Поворот больше этого за один тик — снап, человек так не водит.</summary>
    public const float FlickDegreesPerTick = 45f;

    /// <summary>Сколько снапов на счету, прежде чем считать.</summary>
    public const int FlickStrikeCount = 6;

    /// <summary>Дистанция рукопашного удара, дальше которой бить нечем.</summary>
    public const float MeleeReachMeters = 2.2f;

    /// <summary>Сколько попаданий «сквозь стену или слишком далеко» считаем признаком.</summary>
    public const int ReachStrikeCount = 5;

    // ---------- частота действий ----------

    /// <summary>Окно, за которое считаем действия.</summary>
    public const float ActionWindowSeconds = 1f;

    /// <summary>Действий в секунду выше этого — уже не рука человека.</summary>
    public const float ActionRateLimit = 22f;

    /// <summary>Сколько окон подряд выше лимита, прежде чем считать.</summary>
    public const int ActionStrikeCount = 4;

    // ---------- отклик системы ----------

    /// <summary>Задержки между волнами Karma-наказания, чтобы не сжигать игрока за один метрический всплеск.</summary>
    public static readonly TimeSpan KarmaCooldown = TimeSpan.FromMinutes(2);

    /// <summary>Сколько очков кармы снимаем за подтверждённый признак.</summary>
    public const float KarmaPerFlag = 4f;

    /// <summary>Столько независимых признаков нужно для отключения сессии (только явный спам).</summary>
    public const int FlagsForDisconnect = 3;

    /// <summary>Админ-алерт не чаще раза в столько, на игрока.</summary>
    public static readonly TimeSpan ReportCooldown = TimeSpan.FromMinutes(5);

    // ---------- личность ----------

    /// <summary>Сколько разных аккаунтов с одной /24 считаем «фермой».</summary>
    public const int SameSubnetAccounts = 6;

    /// <summary>Сколько разных аккаунтов с одного legacy-HWID (или без него) считаем обходом.</summary>
    public const int SameWeakHwidAccounts = 4;

    /// <summary>Окно наблюдения для обоих предыдущих пунктов.</summary>
    public static readonly TimeSpan IdentityWindow = TimeSpan.FromHours(6);
}
