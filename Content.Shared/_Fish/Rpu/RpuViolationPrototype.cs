using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Rpu;

/// <summary>
/// Базовая стоимость нарушения в карме.
/// </summary>
[Prototype]
public sealed partial class RpuViolationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Категория нарушения (определяет, какие льготы к нему применяются).</summary>
    [DataField(required: true)]
    public RpuCategory Category;

    /// <summary>Сколько кармы снимается до всех множителей.</summary>
    [DataField(required: true)]
    public float Base;

    /// <summary>Нейтральное описание для служебной записи о начислении кармы.</summary>
    [DataField]
    public string Description = string.Empty;

    /// <summary>Взрывать можно сильно: штраф растёт пропорционально интенсивности.</summary>
    [DataField]
    public bool ScaleWithSeverity;
}
