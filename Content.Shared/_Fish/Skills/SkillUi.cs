using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Skills;

/// <summary>
/// Запись навыка для EUI. Нужен пустой конструктор для сетевой десериализации.
/// </summary>
[Serializable, NetSerializable]
public sealed class SkillUiEntry
{
    public string SkillId = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public string Category = string.Empty;
    public int CurrentLevel;
    public int MinLevel;
    public int MaxLevel;
    public int NextLevelCost;
    public bool CanIncrease;
    public bool CanDecrease;
    public string? BlockReason;
    public string? EffectHint;

    public SkillUiEntry()
    {
    }

    public SkillUiEntry(
        string skillId,
        string name,
        string description,
        string category,
        int currentLevel,
        int minLevel,
        int maxLevel,
        int nextLevelCost,
        bool canIncrease,
        bool canDecrease,
        string? blockReason,
        string? effectHint = null)
    {
        SkillId = skillId;
        Name = name;
        Description = description;
        Category = category;
        CurrentLevel = currentLevel;
        MinLevel = minLevel;
        MaxLevel = maxLevel;
        NextLevelCost = nextLevelCost;
        CanIncrease = canIncrease;
        CanDecrease = canDecrease;
        BlockReason = blockReason;
        EffectHint = effectHint;
    }
}
