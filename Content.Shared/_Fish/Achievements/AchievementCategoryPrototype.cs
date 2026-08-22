using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Fish.Achievements;

/// <summary>
/// Категория достижений для UI-фильтров.
/// </summary>
[Prototype]
public sealed partial class AchievementCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Порядок вкладки в окне достижений.
    /// </summary>
    [DataField]
    public int Order;

    /// <summary>
    /// LocId названия категории.
    /// </summary>
    [DataField(required: true)]
    public LocId Name = string.Empty;

    /// <summary>
    /// Иконка вкладки (state из crt_icons.rsi). Если пусто — medal.
    /// </summary>
    [DataField]
    public string? Icon;
}
