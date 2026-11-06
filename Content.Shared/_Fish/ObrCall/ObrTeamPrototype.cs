using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.ObrCall;

/// <summary>
/// Конфигурация отряда ОБР/РХБЗЗ для вызова через консоли.
/// </summary>
[Prototype]
public sealed partial class ObrTeamPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// LocId названия отряда в UI.
    /// </summary>
    [DataField(required: true)]
    public LocId Name = string.Empty;

    /// <summary>
    /// LocId краткого описания.
    /// </summary>
    [DataField]
    public LocId? Description;

    /// <summary>
    /// GameRule, который загружает шаттл с ролями (LoadMapRule).
    /// </summary>
    [DataField(required: true)]
    public EntProtoId GameRule = string.Empty;

    /// <summary>
    /// Стоимость покупки со станции. null = нельзя купить станцией.
    /// </summary>
    [DataField]
    public int? StationCost;

    /// <summary>
    /// Доступен ли вызов с консоли ЦК.
    /// </summary>
    [DataField]
    public bool CentCommAvailable = true;

    /// <summary>
    /// Доступен ли вызов/покупка с станционной консоли.
    /// </summary>
    [DataField]
    public bool StationAvailable;

    /// <summary>
    /// Счёт станции для списания при покупке.
    /// </summary>
    [DataField]
    public ProtoId<CargoAccountPrototype> Account = "Cargo";

    /// <summary>
    /// PriorityTag для FTL-стыковки (DockFishERT / DockFishCBURN и т.п.).
    /// </summary>
    [DataField]
    public string? PriorityTag;

    /// <summary>
    /// Порядок в UI (меньше = выше).
    /// </summary>
    [DataField]
    public int SortOrder = 100;

    /// <summary>
    /// Спавнеры ролей для развёртывания в криокапсулах ЦК (как по кнопкам мапперов).
    /// Сопоставление с точками SpawnOnTrigger по роли (Leader/Engineer/...).
    /// </summary>
    [DataField]
    public List<EntProtoId> CryoRoleSpawners = new();
}
