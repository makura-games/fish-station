using Robust.Shared;
using Content.Shared._Fish.Rpu;
using Robust.Shared.Configuration;

namespace Content.Shared.CCVar
{
    [CVarDefs]
    public sealed class FishCVars
    {
        /// <summary>
        /// Whether the EORG popup is enabled.
        /// </summary>
        public static readonly CVarDef<bool> EorgPopupEnabled =
            CVarDef.Create("fish.eorg_popup_enabled", true, CVar.SERVER | CVar.REPLICATED);

        /// <summary>
        /// How long to display the EORG popup for.
        /// </summary>
        public static readonly CVarDef<float> EorgPopupTime =
            CVarDef.Create("fish.eorg_popup_time", 5f, CVar.SERVER | CVar.REPLICATED);

        /// <summary>
        /// Message shown in the EORG/volunteer popup.
        /// </summary>
        public static readonly CVarDef<string> EorgPopupMessage =
            CVarDef.Create("fish.eorg_popup_message", "", CVar.SERVER | CVar.REPLICATED);

        /// <summary>
        /// Discord link shown in the EORG/volunteer popup.
        /// </summary>
        public static readonly CVarDef<string> EorgPopupLink =
            CVarDef.Create("fish.eorg_popup_link", "https://discord.com/channels/837289702369263676/1496182871562387667", CVar.SERVER | CVar.REPLICATED);

        /// <summary>
        /// Клиентский CRT-тема для Fish UI (достижения и др.). Архив локально.
        /// </summary>
        public static readonly CVarDef<bool> FishCrtThemeEnabled =
            CVarDef.Create("fish.crt_theme_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

        /// <summary>
        /// CRT-эффекты (scanlines и т.п.). Имеют смысл только при включённой CRT-теме.
        /// </summary>
        public static readonly CVarDef<bool> FishCrtEffectsEnabled =
            CVarDef.Create("fish.crt_effects_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

        /// <summary>
        /// Минимальный Overall playtime (минуты) до записи прогресса в БД. 0 = без порога.
        /// </summary>
        public static readonly CVarDef<int> AchievementsMinOverallPlaytimeMinutes =
            CVarDef.Create("fish.achievements.min_overall_playtime_minutes", 60, CVar.SERVERONLY);

        /// <summary>
        /// Писать в БД только при unlock (progress >= target). Partial — только RAM на сервере.
        /// </summary>
        public static readonly CVarDef<bool> AchievementsPersistOnlyOnUnlock =
            CVarDef.Create("fish.achievements.persist_only_on_unlock", true, CVar.SERVERONLY);

        /// <summary>
        /// Макс. upsert в fish_achievement_progress на игрока за раунд. 0 = без лимита.
        /// </summary>
        public static readonly CVarDef<int> AchievementsMaxDbUpsertsPerRound =
            CVarDef.Create("fish.achievements.max_db_upserts_per_round", 15, CVar.SERVERONLY);

        /// <summary>
        /// Самозапрет взаимодействия агоста по умолчанию (PR #537). На проде true.
        /// </summary>
        public static readonly CVarDef<bool> AghostDefaultInteractionRestricted =
            CVarDef.Create("fish.aghost_default_interaction_restricted", true, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>
        /// Если игрок на loopback (127.0.0.1 / ::1) — не включать самозапрет агоста по умолчанию.
        /// По умолчанию false: reverse-proxy / X-Forwarded-For часто показывают loopback и иначе
        /// сняли бы самозапрет на проде. Локалка: development.toml.
        /// </summary>
        public static readonly CVarDef<bool> AghostUnrestrictOnLocalhost =
            CVarDef.Create("fish.aghost_unrestrict_on_localhost", false, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>
        /// Через сколько секунд после выстрела стреляная гильза исчезает.
        /// Целые патроны не затрагиваются. 0 — исчезнуть на следующем тике после выброса.
        /// </summary>
        public static readonly CVarDef<float> CasingDespawnSeconds =
            CVarDef.Create("fish.casing_despawn_seconds", 60f, CVar.SERVERONLY);
			
			
        // ==== RpuCVars ====
        public static readonly CVarDef<bool> RpuEnabled =
            CVarDef.Create("rpu.enabled", true, CVar.SERVERONLY | CVar.ARCHIVE);

        public static readonly CVarDef<string> RpuAdminMode =
            CVarDef.Create("rpu.admin_mode", "log_only", CVar.SERVERONLY | CVar.ARCHIVE);

        public static readonly CVarDef<string> RpuExemptRanks =
            CVarDef.Create("rpu.exempt_ranks", "", CVar.SERVERONLY | CVar.ARCHIVE);

        public static readonly CVarDef<float> RpuChatLogThreshold =
            CVarDef.Create("rpu.chat_log_threshold", -50f, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>
        /// Сколько очков кармы можно потерять за rpu.spike_minutes, чтобы получить бан
        /// независимо от наигранного времени. 0 — правило выключено.
        /// </summary>
        public static readonly CVarDef<float> RpuSpikeUnits =
            CVarDef.Create("rpu.spike_units", 70f, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>Окно, за которое считается потеря кармы (минуты).</summary>
        public static readonly CVarDef<int> RpuSpikeMinutes =
            CVarDef.Create("rpu.spike_minutes", 10, CVar.SERVERONLY | CVar.ARCHIVE);

        public static readonly CVarDef<float> RpuBaseKarmaPerHour =
            CVarDef.Create("rpu.base_karma_per_hour", 0.25f, CVar.SERVERONLY | CVar.ARCHIVE);

        public static readonly CVarDef<float> RpuRecoveryPerHour =
            CVarDef.Create("rpu.recovery_per_hour", 0.25f, CVar.SERVERONLY | CVar.ARCHIVE);

        public static readonly CVarDef<float> RpuNewbieHours =
            CVarDef.Create("rpu.newbie_hours", 100f, CVar.SERVERONLY | CVar.ARCHIVE);

        public static readonly CVarDef<bool> RpuBanVeterans =
            CVarDef.Create("rpu.ban_veterans", false, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>
        /// Длительность автобана. "0" — навсегда.
        /// Иначе "&lt;число&gt; &lt;единица&gt;": minute/мин, hour/час, day/день, week/неделя, month/месяц, year/год.
        /// Примеры: "0", "6 hours", "3 дня", "1 year".
        /// </summary>
        public static readonly CVarDef<string> RpuBanDuration =
            CVarDef.Create("rpu.ban_duration", "0", CVar.SERVERONLY | CVar.ARCHIVE);

        public static readonly CVarDef<bool> RpuPressureEnabled =
            CVarDef.Create("rpu.pressure.enabled", true, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>Базовая задержка обработки действий нарушителя, мс (растёт до PressureLatencyMaxMs).</summary>
        public static readonly CVarDef<int> RpuPressureLatencyMs =
            CVarDef.Create("rpu.pressure.latency_ms", 50, CVar.SERVERONLY | CVar.ARCHIVE);

        public static readonly CVarDef<int> RpuPressureLatencyMaxMs =
            CVarDef.Create("rpu.pressure.latency_max_ms", 400, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>Базовый шанс «не зарегистрированного» действия/попадания (растёт до PressureMissMaxChance).</summary>
        public static readonly CVarDef<float> RpuPressureMissChance =
            CVarDef.Create("rpu.pressure.miss_chance", 0.15f, CVar.SERVERONLY | CVar.ARCHIVE);

        public static readonly CVarDef<float> RpuPressureMissMaxChance =
            CVarDef.Create("rpu.pressure.miss_max_chance", 0.5f, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>Карма, ниже которой начинают работать эффекты давления.</summary>
        public static readonly CVarDef<float> RpuPressureStartKarma =
            CVarDef.Create("rpu.pressure.start_karma", -1f, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>Шанс случайно подкоситься на ровном месте (0 — выключено).</summary>
        public static readonly CVarDef<float> RpuPressureSlipChance =
            CVarDef.Create("rpu.pressure.slip_chance", 0.02f, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>Шанс осечки оружия (0 — выключено).</summary>
        public static readonly CVarDef<float> RpuPressureJamChance =
            CVarDef.Create("rpu.pressure.jam_chance", 0.08f, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>Шанс того, что предмет выскользнет из рук (0 — выключено).</summary>
        public static readonly CVarDef<float> RpuPressureDropChance =
            CVarDef.Create("rpu.pressure.drop_chance", 0.05f, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>Отбрасывание на прошлую позицию (заметно, по умолчанию выключено).</summary>
        public static readonly CVarDef<bool> RpuPressureRubberband =
            CVarDef.Create("rpu.pressure.rubberband", false, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>Ложные срабатывания сигнализации на нарушителя (по умолчанию выключено).</summary>
        public static readonly CVarDef<bool> RpuPressureFalseAlarm =
            CVarDef.Create("rpu.pressure.false_alarm", false, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>Сколько контрабандных предметов считается «избыточным».</summary>
        public static readonly CVarDef<int> RpuContrabandItemLimit =
            CVarDef.Create("rpu.contraband.item_limit", 3, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>Сколько минут контрабанда должна пролежать в инвентаре перед штрафом.</summary>
        public static readonly CVarDef<float> RpuContrabandDwellMinutes =
            CVarDef.Create("rpu.contraband.dwell_minutes", 12f, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>Порог разлива опасных веществ, ед.</summary>
        public static readonly CVarDef<float> RpuSpillUnitThreshold =
            CVarDef.Create("rpu.spill.unit_threshold", 60f, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>Расстояние «без присмотра» для сингулярности/теслы, тайлы.</summary>
        public static readonly CVarDef<int> RpuSingularityWatchTiles =
            CVarDef.Create("rpu.singularity.watch_tiles", 50, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>Сколько минут сингулярность/тесла стоит без присмотра перед штрафом.</summary>
        public static readonly CVarDef<float> RpuSingularityWatchMinutes =
            CVarDef.Create("rpu.singularity.watch_minutes", 30f, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>
        /// Потолок надбавки: сколько очков можно добавить к базовой цене нарушения
        /// отягчающими поправками (новичок, серия убийств, прежние баны).
        /// </summary>
        public static readonly CVarDef<float> RpuMaxExtraUnits =
            CVarDef.Create("rpu.penalty.max_extra", RpuMaths.MaxExtraPenalty, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>
        /// Минимальное снятие очков за вменяемое нарушение (0.1 — одна десятая очка).
        /// Полное освобождение задаётся не этим значением, а списком waives в rpuAdjust.
        /// </summary>
        public static readonly CVarDef<float> RpuMinPenalty =
            CVarDef.Create("rpu.penalty.min", RpuMaths.MinPenalty, CVar.SERVERONLY | CVar.ARCHIVE);

        // ==== PmCVars ====
        /// <summary>Включён ли сбор метрик по сессиям.</summary>
        public static readonly CVarDef<bool> PerfmonEnabled =
            CVarDef.Create("perfmon.enabled", true, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>Учитывать ли выбросы метрик при корректировке кармы сессии.</summary>
        public static readonly CVarDef<bool> PerfmonKarma =
            CVarDef.Create("perfmon.karma", true, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>Разрывать ли соединение при устойчивом превышении частоты обращений.</summary>
        public static readonly CVarDef<bool> PerfmonDisconnect =
            CVarDef.Create("perfmon.disconnect", true, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>Показывать ли сводку по сессии в админ-чате.</summary>
        public static readonly CVarDef<bool> PerfmonReport =
            CVarDef.Create("perfmon.report", true, CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>
        /// Расстояние, дальше которого клиент уже не мог увидеть цель, в тайлах.
        /// Должно соответствовать реальной видимости на сервере (настройкам PVS/обзора):
        /// метрика сравнивает дистанцию удара именно с этим значением.
        /// </summary>
        public static readonly CVarDef<float> PerfmonPerceiveMeters =
            CVarDef.Create("perfmon.perceive_meters", 12f, CVar.SERVERONLY | CVar.ARCHIVE);

        // ==== FishVisibilityCVars ====
        /// <summary>
        /// Имена прототипов, чьё состояние не отправляется обычным игрокам: запятые или пробелы.
        /// Такие сущности уходят только тем, у кого в маске глаза есть слой администратора
        /// или наблюдателя. Игроков и их тела система прятать отказывается.
        /// </summary>
        public static readonly CVarDef<string> VisibilityAdminOnlyPrototypes =
            CVarDef.Create("fish.visibility.admin_only_prototypes", "", CVar.SERVERONLY | CVar.ARCHIVE);

        /// <summary>Применять список вообще.</summary>
        public static readonly CVarDef<bool> VisibilityLayerEnabled =
            CVarDef.Create("fish.visibility.enabled", true, CVar.SERVERONLY | CVar.ARCHIVE);
    }
}
