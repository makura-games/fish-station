using Robust.Shared.Configuration;

namespace Content.Shared._Fish.Rpu;

/// <summary>
/// Настройки режима персонального угнетения. Только серверные: система работает втайне от игроков.
/// </summary>
[CVarDefs]
public static class RpuCVars
{
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("rpu.enabled", true, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> AdminMode =
        CVarDef.Create("rpu.admin_mode", "log_only", CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> ExemptRanks =
        CVarDef.Create("rpu.exempt_ranks", "", CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> ChatLogThreshold =
        CVarDef.Create("rpu.chat_log_threshold", -50f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> BaseKarmaPerHour =
        CVarDef.Create("rpu.base_karma_per_hour", 0.25f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> RecoveryPerHour =
        CVarDef.Create("rpu.recovery_per_hour", 0.25f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> NewbieHours =
        CVarDef.Create("rpu.newbie_hours", 100f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> BanVeterans =
        CVarDef.Create("rpu.ban_veterans", false, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Длительность автобана. "0" — навсегда.
    /// Иначе "&lt;число&gt; &lt;единица&gt;": minute/мин, hour/час, day/день, week/неделя, month/месяц, year/год.
    /// Примеры: "0", "6 hours", "3 дня", "1 year".
    /// </summary>
    public static readonly CVarDef<string> BanDuration =
        CVarDef.Create("rpu.ban_duration", "0", CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> PressureEnabled =
        CVarDef.Create("rpu.pressure.enabled", true, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Базовая задержка обработки действий нарушителя, мс (растёт до PressureLatencyMaxMs).</summary>
    public static readonly CVarDef<int> PressureLatencyMs =
        CVarDef.Create("rpu.pressure.latency_ms", 50, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> PressureLatencyMaxMs =
        CVarDef.Create("rpu.pressure.latency_max_ms", 400, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Базовый шанс «не зарегистрированного» действия/попадания (растёт до PressureMissMaxChance).</summary>
    public static readonly CVarDef<float> PressureMissChance =
        CVarDef.Create("rpu.pressure.miss_chance", 0.15f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> PressureMissMaxChance =
        CVarDef.Create("rpu.pressure.miss_max_chance", 0.5f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Карма, ниже которой начинают работать эффекты давления.</summary>
    public static readonly CVarDef<float> PressureStartKarma =
        CVarDef.Create("rpu.pressure.start_karma", -1f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Шанс случайно подкоситься на ровном месте (0 — выключено).</summary>
    public static readonly CVarDef<float> PressureSlipChance =
        CVarDef.Create("rpu.pressure.slip_chance", 0.02f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Шанс осечки оружия (0 — выключено).</summary>
    public static readonly CVarDef<float> PressureJamChance =
        CVarDef.Create("rpu.pressure.jam_chance", 0.08f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Шанс того, что предмет выскользнет из рук (0 — выключено).</summary>
    public static readonly CVarDef<float> PressureDropChance =
        CVarDef.Create("rpu.pressure.drop_chance", 0.05f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Насколько больнее нарушителю получать урон (1.0 — без изменений).</summary>
    public static readonly CVarDef<float> PressureIncomingDamage =
        CVarDef.Create("rpu.pressure.incoming_damage", 1.08f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Отбрасывание на прошлую позицию (заметно, по умолчанию выключено).</summary>
    public static readonly CVarDef<bool> PressureRubberband =
        CVarDef.Create("rpu.pressure.rubberband", false, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Ложные срабатывания сигнализации на нарушителя (по умолчанию выключено).</summary>
    public static readonly CVarDef<bool> PressureFalseAlarm =
        CVarDef.Create("rpu.pressure.false_alarm", false, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Сколько контрабандных предметов считается «избыточным».</summary>
    public static readonly CVarDef<int> ContrabandItemLimit =
        CVarDef.Create("rpu.contraband.item_limit", 3, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Сколько минут контрабанда должна пролежать в инвентаре перед штрафом.</summary>
    public static readonly CVarDef<float> ContrabandDwellMinutes =
        CVarDef.Create("rpu.contraband.dwell_minutes", 12f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Порог разлива опасных веществ, ед.</summary>
    public static readonly CVarDef<float> SpillUnitThreshold =
        CVarDef.Create("rpu.spill.unit_threshold", 60f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Расстояние «без присмотра» для сингулярности/теслы, тайлы.</summary>
    public static readonly CVarDef<int> SingularityWatchTiles =
        CVarDef.Create("rpu.singularity.watch_tiles", 50, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Сколько минут сингулярность/тесла стоит без присмотра перед штрафом.</summary>
    public static readonly CVarDef<float> SingularityWatchMinutes =
        CVarDef.Create("rpu.singularity.watch_minutes", 30f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Затухание весов при комбинировании льгот (веса 1, decay, decay^2, ...).</summary>
    public static readonly CVarDef<float> ModifierWeightDecay =
        CVarDef.Create("rpu.modifier.weight_decay", 0.6f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Нижняя граница итогового множителя штрафа.</summary>
    public static readonly CVarDef<float> ModifierFloor =
        CVarDef.Create("rpu.modifier.floor", 0.15f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Верхняя граница итогового множителя штрафа.</summary>
    public static readonly CVarDef<float> ModifierCap =
        CVarDef.Create("rpu.modifier.cap", 1.5f, CVar.SERVERONLY | CVar.ARCHIVE);
}
