using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Skills;

/// <summary>
/// Прототип навыка Fish Station: стоимость уровней, таблицы скорости и эффективности.
/// </summary>
[Prototype]
public sealed partial class SkillPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name = string.Empty;

    [DataField]
    public LocId Description = string.Empty;

    [DataField]
    public string Category = "general";

    /// <summary>
    /// Сложность (UI / метаданные). На лестницу стоимости очков не влияет.
    /// </summary>
    [DataField]
    public int Difficulty = SkillLevels.DifficultyAverage;

    [DataField]
    public int DefaultMax = SkillLevels.Trained;

    [DataField]
    public Dictionary<ProtoId<SkillPrototype>, int> Prerequisites = new();

    [DataField]
    public float DelayFactor = 0.3f;

    [DataField]
    public float QualityFactor = 0.15f;

    /// <summary>
    /// Множители длительности по уровню (меньше = быстрее). Пусто → DelayFactor.
    /// </summary>
    [DataField]
    public Dictionary<int, float> DurationMultipliers = new();

    /// <summary>
    /// Множители качества/эффективности по уровню. Пусто → QualityFactor.
    /// </summary>
    [DataField]
    public Dictionary<int, float> QualityMultipliers = new();

    public int GetCostForLevel(int level) => SkillMath.GetCostForLevel(Difficulty, level);

    public int GetLevelCost(int fromLevel, int toLevel) => SkillMath.GetLevelCost(Difficulty, fromLevel, toLevel);

    public float GetDurationMultiplierForLevel(int level) =>
        SkillMath.GetDurationMultiplier(DurationMultipliers, DelayFactor, level);

    public float GetQualityMultiplierForLevel(int level) =>
        SkillMath.GetQualityMultiplier(QualityMultipliers, QualityFactor, level);
}
