using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Rpu;

/// <summary>
/// Абсолютные освобождения и служебные пороги системы.
/// </summary>
[Prototype]
public sealed partial class RpuTuningPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Антагонисты, чья смерть не стоит никому кармы (крупные анты и угрозы фауны).
    /// </summary>
    [DataField]
    public HashSet<ProtoId<AntagPrototype>> FreeKillAntags = new();

    /// <summary>
    /// Мелкие антагонисты: карму не снимают, если они напали первыми.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<AntagPrototype>> FirstStrikeAntags = new();

    /// <summary>
    /// Антагонисты, полностью освобождённые от системы (животные, орды, зерга).
    /// </summary>
    [DataField]
    public HashSet<ProtoId<AntagPrototype>> ImmuneAntags = new();

    /// <summary>
    /// Должности, полностью освобождённые от системы.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<JobPrototype>> ImmuneJobs = new();

    /// <summary>
    /// Прототипы мобов, полностью освобождённые от системы (животные). Pun Pun здесь не будет.
    /// </summary>
    [DataField]
    public HashSet<string> ImmuneMobs = new();

    /// <summary>
    /// Режимы, в которых система не работает вовсе.
    /// </summary>
    [DataField]
    public HashSet<string> DisabledGamePresets = new();

    /// <summary>Пороги автобана по часам игры на должностях.</summary>
    [DataField]
    public List<RpuBanTier> BanTiers = new();

    /// <summary>Сколько очков сверху к штрафу для новичка (меньше NewbieHours наиграно).</summary>
    [DataField]
    public float NewbieUnits = 2f;

    [DataField]
    public float NewbieHours = 10f;

    /// <summary>Сколько очков сверху для совсем нового игрока (меньше VeryNewHours).</summary>
    [DataField]
    public float VeryNewUnits = 3f;

    /// <summary>До скольки часов считаем «совсем новым».</summary>
    [DataField]
    public float VeryNewHours = 3f;

    /// <summary>Очки сверху за одиночного антагониста на чистой станции.</summary>
    [DataField]
    public float LonelyAntagUnits = 1f;
    /// <summary>Поправки по количеству банов за последние BanHistoryDays дней (в очках).</summary>
    [DataField]
    public List<RpuBanHistoryTier> BanHistoryTiers = new();

    [DataField]
    public int BanHistoryDays = 180;

    /// <summary>
    /// Должности, чья гибель стоит дороже: командование, СБ, ОБР, РХБЗЗ, ЦК.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<JobPrototype>> SecurityChainJobs = new();

    /// <summary>Отделы того же ряда (на случай кастомных должностей).</summary>
    [DataField]
    public HashSet<ProtoId<DepartmentPrototype>> SecurityChainDepartments = new();

    /// <summary>Прототипы мобов, гибель которых стоит дороже (Pun Pun).</summary>
    [DataField]
    public HashSet<string> HighValueMobs = new();

    /// <summary>Доступы «высокого» уровня: командование, СБ, арсенал, ЦК.</summary>
    [DataField]
    public HashSet<string> HighSecurityAccess = new();

    /// <summary>Отделовые доступы: наука, инженерия, медицина, радио, ботаника и т. п.</summary>
    [DataField]
    public HashSet<string> DepartmentAccess = new();

    /// <summary>Служебные доступы: сервис, кладовка, кухня, бар.</summary>
    [DataField]
    public HashSet<string> ServiceAccess = new();
}
