using Robust.Shared;
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
    }
}
