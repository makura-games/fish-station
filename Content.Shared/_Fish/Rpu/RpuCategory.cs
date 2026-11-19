namespace Content.Shared._Fish.Rpu;

/// <summary>
/// Категория нарушения, за которое система снимает карму.
/// </summary>
public enum RpuCategory
{
    /// <summary>Убийство обычного члена экипажа.</summary>
    Kill,

    /// <summary>Убийство командования, СБ, ОБР, РХБЗЗ или ЦК.</summary>
    KillSecurityChain,

    /// <summary>Доведение до критического состояния.</summary>
    Crit,

    /// <summary>Взрыв.</summary>
    Explosion,

    /// <summary>Взлом шлюза (провода, emag, взломщик доступа).</summary>
    AirlockHack,

    /// <summary>Перерезанные провода.</summary>
    WireCut,

    /// <summary>Разрушение стен, окон или шлюзов.</summary>
    Destruction,

    /// <summary>Разбор (deconstruction) тех же объектов.</summary>
    Deconstruction,

    /// <summary>Саботаж атмоса, труб, разгерметизация.</summary>
    Sabotage,

    /// <summary>Разлив лекарственных/наркотических/токсичных/пиротехнических веществ.</summary>
    Spill,

    /// <summary>Кто-то подскользнулся на оставленной игроком кожуре или мыле.</summary>
    Slip,

    /// <summary>Сингулярность или тесла без присмотра либо сбежавшие.</summary>
    Singularity,

    /// <summary>Длительно ношеная контрабанда.</summary>
    Contraband,
}

/// <summary>
/// Откуда берётся множитель: должностной, антагонистный, ситуативный.
/// </summary>
public enum RpuModifierSource
{
    Job = 0,
    Department = 1,
    Antag = 2,
    /// <summary>Обстановка на станции (кто из антагонистов активен). Только для самих антагонистов.</summary>
    StationSituation = 3,
    /// <summary>Код тревоги.</summary>
    AlertLevel = 4,
    /// <summary>Счётчик убийств за раунд.</summary>
    KillCount = 5,
    /// <summary>Наигранное время (новички получают больше).</summary>
    Playtime = 6,
    /// <summary>История банов (усиливает штраф).</summary>
    BanHistory = 7,
}

/// <summary>
/// Режим применения системы к действующим админам.
/// </summary>
public enum RpuAdminMode
{
    Off,
    Normal,
    LogOnly,
}
