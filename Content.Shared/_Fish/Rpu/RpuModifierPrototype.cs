using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Rpu;

/// <summary>
/// Набор множителей штрафа: роль, отдел, антагонист, обстановка на станции, код тревоги.
/// </summary>
[Prototype]
public sealed partial class RpuModifierPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public RpuModifierSource Source;

    /// <summary>Должности, при которых модификатор подходит (достаточно одного совпадения).</summary>
    [DataField]
    public HashSet<ProtoId<JobPrototype>> Jobs = new();

    /// <summary>Отделы, при которых модификатор подходит.</summary>
    [DataField]
    public HashSet<ProtoId<DepartmentPrototype>> Departments = new();

    /// <summary>
    /// Антагонисты: для Source=Antag совпадает роль подозреваемого,
    /// для Source=StationSituation — роль, чьё присутствие на станции включает Modifier.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<AntagPrototype>> Antags = new();

    /// <summary>Коды тревоги (green, blue, yellow, red, gamma, delta, epsilon, sierra).</summary>
    [DataField]
    public HashSet<string> AlertLevels = new();

    /// <summary>Применять только если подозреваемый сам является антагонистом.</summary>
    [DataField]
    public bool AntagonistsOnly;

    /// <summary>Множители по категориям, ключ — имя RpuCategory.</summary>
    [DataField]
    public Dictionary<string, float> Categories = new();

    /// <summary>Множитель для категорий, которых нет в Categories (применяется только если ApplyDefault = true).</summary>
    [DataField]
    public float Default = 1f;

    [DataField]
    public bool ApplyDefault;

    public bool TryGetMultiplier(RpuCategory category, out float multiplier)
    {
        if (Categories.TryGetValue(category.ToString(), out var value))
        {
            multiplier = value;
            return true;
        }

        multiplier = ApplyDefault ? Default : 1f;
        return ApplyDefault && Default != 1f;
    }
}
