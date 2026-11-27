using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Skills;

/// <summary>
/// Формулы стоимости и модификаторов навыков Fish Station.
/// Лестница стоимости подобрана так, чтобы мастер одного навыка
/// почти исчерпывал бюджет специалиста, а универсал оставался на средних уровнях.
/// </summary>
public static class SkillMath
{
    /// <summary>1→2</summary>
    public const int CostBasic = 2;

    /// <summary>2→3</summary>
    public const int CostTrained = 3;

    /// <summary>3→4</summary>
    public const int CostExperienced = 5;

    /// <summary>4→5</summary>
    public const int CostMaster = 8;

    /// <summary>Полный путь с необученного до мастера.</summary>
    public const int CostUnskilledToMaster = CostBasic + CostTrained + CostExperienced + CostMaster; // 18

    /// <summary>С необученного до опытного (потолок обучения книгами).</summary>
    public const int CostUnskilledToExperienced = CostBasic + CostTrained + CostExperienced; // 10

    /// <summary>Максимальный уровень, достижимый обучением по книгам.</summary>
    public const int BookTrainingMaxLevel = SkillLevels.Experienced;

    public static int GetCostForLevel(int difficulty, int level)
    {
        // difficulty сохранён в API для совместимости прототипов; на стоимость не влияет.
        _ = difficulty;
        return level switch
        {
            SkillLevels.Basic => CostBasic,
            SkillLevels.Trained => CostTrained,
            SkillLevels.Experienced => CostExperienced,
            SkillLevels.Master => CostMaster,
            _ => 0,
        };
    }

    public static int GetLevelCost(int difficulty, int fromLevel, int toLevel)
    {
        if (toLevel <= fromLevel)
            return 0;

        var sum = 0;
        for (var i = fromLevel + 1; i <= toLevel; i++)
            sum += GetCostForLevel(difficulty, i);

        return sum;
    }

    public static float GetDurationMultiplier(IReadOnlyDictionary<int, float> table, float delayFactor, int level)
    {
        level = Math.Clamp(level, SkillLevels.Min, SkillLevels.Max);

        if (table.TryGetValue(level, out var explicitMult))
            return Math.Max(0f, explicitMult);

        return Math.Max(0f, 1f + (SkillLevels.Baseline - level) * delayFactor);
    }

    public static float GetQualityMultiplier(IReadOnlyDictionary<int, float> table, float qualityFactor, int level)
    {
        level = Math.Clamp(level, SkillLevels.Min, SkillLevels.Max);

        if (table.TryGetValue(level, out var explicitMult))
            return Math.Max(0.001f, explicitMult);

        return Math.Max(0.05f, 1f + (level - SkillLevels.Baseline) * qualityFactor);
    }

    /// <summary>Ключ прогресса обучения: навык + целевой уровень.</summary>
    public static string MakeTrainingKey(ProtoId<SkillPrototype> skill, int targetLevel) =>
        $"{skill}:{targetLevel}";
}

