using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Rpu;

/// <summary>
/// Поправка в очках кармы: сколько очков прибавить (плюс) или вычесть (минус)
/// за нарушение определённого вида конкретным ролям, отделам и обстановке.
/// </summary>
[Prototype]
public sealed partial class RpuAdjustPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Откуда поправка: должность, отдел, роль антагониста, обстановка, код тревоги.</summary>
    [DataField(required: true)]
    public RpuAdjustSource Source;

    [DataField]
    public HashSet<ProtoId<JobPrototype>> Jobs = new();

    [DataField]
    public HashSet<ProtoId<DepartmentPrototype>> Departments = new();

    [DataField]
    public HashSet<ProtoId<AntagPrototype>> Antags = new();

    /// <summary>Уровни тревоги, при которых действует поправка.</summary>
    [DataField]
    public HashSet<string> AlertLevels = new();

    /// <summary>
    /// Категории, за которые игрок вообще не отвечает: с него не снимается ничего.
    /// </summary>
    [DataField]
    public HashSet<string> Waives = new();

    /// <summary>
    /// Поправка в очках по категориям нарушений. Плюсом — отягчающие, минусом — смягчающие.
    /// Категории, которой здесь нет, поправки не получают.
    /// </summary>
    [DataField]
    public Dictionary<string, float> Units = new();

    /// <summary>Действует только на тех, кто сейчас антагонист.</summary>
    [DataField]
    public bool OnlyForAntagonists;

    /// <summary>Сколько очков добавляет (или вычитает) эта поправка за данную категорию.</summary>
    public bool TryGetUnits(RpuCategory category, out float units)
    {
        return Units.TryGetValue(category.ToString(), out units);
    }

    /// <summary>Вменяется ли игроку данная категория нарушений вообще.</summary>
    public bool WaivesCategory(RpuCategory category)
    {
        return Waives.Contains(category.ToString());
    }
}
