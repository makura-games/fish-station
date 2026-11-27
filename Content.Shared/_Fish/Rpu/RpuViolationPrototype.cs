using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Rpu;

/// <summary>
/// Базовая цена нарушения в очках кармы.
/// </summary>
[Prototype]
public sealed partial class RpuViolationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Категория нарушения (определяет, какие льготы к нему применяются).</summary>
    [DataField(required: true)]
    public RpuCategory Category;

    /// <summary>Сколько очков кармы снимается за нарушение до всех поправок.</summary>
    [DataField(required: true)]
    public float Base;

    /// <summary>Нейтральное описание для служебной записи о начислении кармы.</summary>
    [DataField]
    public string Description = string.Empty;
}
