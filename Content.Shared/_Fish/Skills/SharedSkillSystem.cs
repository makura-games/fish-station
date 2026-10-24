using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Skills;

/// <summary>
/// Общий API навыков Fish Station: уровень, проверка, множители времени и качества.
/// </summary>
public abstract partial class SharedSkillSystem : EntitySystem
{
    public const int DefaultJobSkillPoints = 14;

    [Dependency] protected IPrototypeManager Prototypes = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeTraining();
    }

    public int GetSkillValue(EntityUid uid, ProtoId<SkillPrototype> skill, SkillsetComponent? skillset = null)
    {
        // Без skillset (DeathMatch и т.п.) — нейтральный Baseline, без штрафов Unskilled.
        if (!Resolve(uid, ref skillset, false))
            return SkillLevels.Baseline;

        return skillset.Skills.GetValueOrDefault(skill, SkillLevels.Unskilled);
    }

    /// <summary>
    /// Уровень навыка для игровых эффектов. До Confirm (<see cref="SkillsetComponent.Locked"/>)
    /// учитывается только минимум роли — provisional-распределение очков на действия не влияет.
    /// </summary>
    public int GetEffectiveSkillValue(EntityUid uid, ProtoId<SkillPrototype> skill, SkillsetComponent? skillset = null)
    {
        if (!Resolve(uid, ref skillset, false))
            return SkillLevels.Baseline;

        if (!skillset.Locked)
        {
            if (skillset.JobId is { } jobId)
                return GetMinSkill(jobId, skill);

            return SkillLevels.Unskilled;
        }

        return skillset.Skills.GetValueOrDefault(skill, SkillLevels.Unskilled);
    }

    public bool SkillCheck(EntityUid uid, ProtoId<SkillPrototype> skill, int needed, SkillsetComponent? skillset = null)
    {
        return GetEffectiveSkillValue(uid, skill, skillset) >= needed;
    }

    public float GetSkillDelayMultiplier(EntityUid uid, ProtoId<SkillPrototype> skill, float? factorOverride = null, SkillsetComponent? skillset = null)
    {
        var points = GetEffectiveSkillValue(uid, skill, skillset);

        if (Prototypes.TryIndex(skill, out SkillPrototype? proto))
        {
            if (proto.DurationMultipliers.Count > 0)
                return proto.GetDurationMultiplierForLevel(points);

            var factor = factorOverride ?? proto.DelayFactor;
            return SkillMath.GetDurationMultiplier(proto.DurationMultipliers, factor, points);
        }

        var fallbackFactor = factorOverride ?? 0.3f;
        return SkillMath.GetDurationMultiplier(_emptySkillTable, fallbackFactor, points);
    }

    public float GetSkillQualityMultiplier(EntityUid uid, ProtoId<SkillPrototype> skill, SkillsetComponent? skillset = null)
    {
        var points = GetEffectiveSkillValue(uid, skill, skillset);

        if (Prototypes.TryIndex(skill, out SkillPrototype? proto))
            return proto.GetQualityMultiplierForLevel(points);

        return SkillMath.GetQualityMultiplier(_emptySkillTable, 0.15f, points);
    }

    private static readonly Dictionary<int, float> _emptySkillTable = new();

    public TimeSpan ApplySkillDelay(EntityUid uid, ProtoId<SkillPrototype> skill, TimeSpan baseDelay, float? factorOverride = null)
    {
        return baseDelay * GetSkillDelayMultiplier(uid, skill, factorOverride);
    }

    public bool TryGetBudget(ProtoId<JobPrototype> job, [NotNullWhen(true)] out JobSkillBudgetPrototype? budget)
    {
        if (Prototypes.TryIndex<JobSkillBudgetPrototype>(job, out budget))
            return true;

        foreach (var proto in Prototypes.EnumeratePrototypes<JobSkillBudgetPrototype>())
        {
            if (proto.GetJobId() == job)
            {
                budget = proto;
                return true;
            }
        }

        budget = null;
        return false;
    }

    public int GetJobSkillPoints(ProtoId<JobPrototype> job)
    {
        return TryGetBudget(job, out var budget) ? Math.Max(0, budget.SkillPoints) : DefaultJobSkillPoints;
    }

    public int GetMinSkill(ProtoId<JobPrototype> job, ProtoId<SkillPrototype> skill)
    {
        if (TryGetBudget(job, out var budget) && budget.MinSkills.TryGetValue(skill, out var min))
            return Math.Clamp(min, SkillLevels.Min, SkillLevels.Max);

        return SkillLevels.Min;
    }

    public int GetMaxSkill(ProtoId<JobPrototype> job, ProtoId<SkillPrototype> skill)
    {
        var min = GetMinSkill(job, skill);
        if (TryGetBudget(job, out var budget) && budget.MaxSkills.TryGetValue(skill, out var max))
            return Math.Max(min, Math.Clamp(max, SkillLevels.Min, SkillLevels.Max));

        if (Prototypes.TryIndex(skill, out var proto))
            return Math.Max(min, Math.Clamp(proto.DefaultMax, SkillLevels.Min, SkillLevels.Max));

        return Math.Max(min, SkillLevels.Trained);
    }

    public bool CheckPrerequisites(EntityUid uid, SkillPrototype skill, SkillsetComponent skillset)
    {
        foreach (var (reqSkill, reqLevel) in skill.Prerequisites)
        {
            if (GetSkillValue(uid, reqSkill, skillset) < reqLevel)
                return false;
        }

        return true;
    }

    public bool TryDenyMissingSkill(
        EntityUid user,
        ProtoId<SkillPrototype> skill,
        int needed,
        string failLocId,
        SkillsetComponent? skillset = null)
    {
        if (SkillCheck(user, skill, needed, skillset))
            return false;

        var skillName = Prototypes.TryIndex(skill, out var proto)
            ? Loc.GetString(proto.Name)
            : skill.ToString();

        _popup.PopupEntity(
            Loc.GetString(failLocId,
                ("skill", skillName),
                ("level", Loc.GetString(SkillLevels.GetLevelNameKey(needed)))),
            user,
            user);
        return true;
    }

    public string FormatEffectHint(ProtoId<SkillPrototype> skill, int level)
    {
        if (!Prototypes.TryIndex(skill, out SkillPrototype? proto))
            return string.Empty;

        var delay = proto.GetDurationMultiplierForLevel(level);
        var quality = proto.GetQualityMultiplierForLevel(level);
        var hasDuration = proto.DurationMultipliers.Count > 0 || Math.Abs(delay - 1f) > 0.001f;
        var hasQuality = proto.QualityMultipliers.Count > 0 || Math.Abs(quality - 1f) > 0.001f;

        if (hasDuration && hasQuality)
        {
            return Loc.GetString("skills-effect-delay-and-quality",
                ("delay", FormatPercent(delay)),
                ("quality", FormatPercent(quality)));
        }

        if (hasQuality)
            return Loc.GetString("skills-effect-quality", ("quality", FormatPercent(quality)));

        if (hasDuration)
            return Loc.GetString("skills-effect-delay", ("delay", FormatPercent(delay)));

        return string.Empty;
    }

    private static string FormatPercent(float mult) => $"{Math.Round(mult * 100f):0}%";
}

