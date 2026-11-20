using Robust.Shared.Configuration;

namespace Content.Shared._Fish.PerformanceMetrics;

/// <summary>
/// Настройки сбора метрик по сессиям. Только серверные: значения не уезжают клиентам.
/// </summary>
[CVarDefs]
// Fish-start
public static class PmCVars
{
    /// <summary>Включён ли сбор метрик по сессиям.</summary>
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("perfmon.enabled", true, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Учитывать ли выбросы метрик при корректировке кармы сессии.</summary>
    public static readonly CVarDef<bool> Karma =
        CVarDef.Create("perfmon.karma", true, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Разрывать ли соединение при устойчивом превышении частоты обращений.</summary>
    public static readonly CVarDef<bool> Disconnect =
        CVarDef.Create("perfmon.disconnect", false, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Показывать ли сводку по сессии в админ-чате.</summary>
    public static readonly CVarDef<bool> Report =
        CVarDef.Create("perfmon.report", true, CVar.SERVERONLY | CVar.ARCHIVE);
}
// Fish-end
