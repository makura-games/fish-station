using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Fish.Shipyard;

/// <summary>
/// Прототип шаттла, доступного для покупки на консоли верфи.
/// </summary>
[Prototype]
public sealed partial class VesselPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Отображаемое имя (LocId или готовая строка).
    /// </summary>
    [DataField(required: true)]
    public string Name = string.Empty;

    /// <summary>
    /// Краткое описание для UI.
    /// </summary>
    [DataField]
    public string Description = string.Empty;

    /// <summary>
    /// Стоимость покупки со счёта станции.
    /// </summary>
    [DataField(required: true)]
    public int Price;

    /// <summary>
    /// Путь к grid-карте шаттла.
    /// </summary>
    [DataField(required: true)]
    public ResPath ShuttlePath = default!;

    /// <summary>
    /// Группа каталога (фильтр на консоли).
    /// </summary>
    [DataField]
    public string Group = "civilian";

    /// <summary>
    /// Предпочтительный тег PriorityDock при стыковке (например DockPersonal).
    /// </summary>
    [DataField]
    public string? PriorityDockTag;
}
