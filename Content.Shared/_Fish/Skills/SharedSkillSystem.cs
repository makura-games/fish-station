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

    /// <summary>
    /// Пол «обычного человека» для навыков без явного minSkills роли.
    /// Иначе skillset роняет всё в Unskilled и делает хуже сущностей без skillset (Baseline).
    /// </summary>
    public const int HumanSkillFloor = SkillLevels.Baseline;

    private static readonly Dictionary<int, float> _emptySkillTable = new();

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
    /// Уровень навыка для игровых эффектов.
    /// Учитывает текущее распределение сразу — Confirm только закрепляет очки и открывает учебники.
    /// </summary>
    public int GetEffectiveSkillValue(EntityUid uid, ProtoId<SkillPrototype> skill, SkillsetComponent? skillset = null)
    {
        if (!Resolve(uid, ref skillset, false))
            return SkillLevels.Baseline;

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

        return HumanSkillFloor;
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

    /// <summary>
    /// Текст недостающих требований для UI. null — если всё выполнено.
    /// </summary>
    public string? FormatMissingPrerequisites(EntityUid uid, SkillPrototype skill, SkillsetComponent skillset)
    {
        if (skill.Prerequisites.Count == 0)
            return null;

        List<string>? missing = null;
        foreach (var (reqSkill, reqLevel) in skill.Prerequisites)
        {
            if (GetSkillValue(uid, reqSkill, skillset) >= reqLevel)
                continue;

            missing ??= new List<string>();
            var name = Prototypes.TryIndex(reqSkill, out var proto)
                ? Loc.GetString(proto.Name)
                : reqSkill.ToString();
            missing.Add(Loc.GetString("skills-error-prerequisite-item",
                ("skill", name),
                ("level", Loc.GetString(SkillLevels.GetLevelNameKey(reqLevel)))));
        }

        if (missing is not { Count: > 0 })
            return null;

        return Loc.GetString("skills-error-prerequisites-detail",
            ("requirements", string.Join(", ", missing)));
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

    public string FormatAffectsHint(SkillPrototype proto)
    {
        // LocId? — пустой LocId даёт пустую строку; IsNullOrWhiteSpace через ToString.
        if (proto.UiAffects is { } affects)
        {
            var key = affects.ToString();
            if (!string.IsNullOrWhiteSpace(key))
                return Loc.GetString(key);
        }

        return Loc.GetString("skills-affects-none");
    }

    public string FormatEffectHint(ProtoId<SkillPrototype> skill, int level)
    {
        if (!Prototypes.TryIndex(skill, out SkillPrototype? proto))
            return string.Empty;

        var delay = proto.GetDurationMultiplierForLevel(level);
        var quality = proto.GetQualityMultiplierForLevel(level);
        // Есть ли у навыка реальные таблицы/факторы — иначе UI не врёт процентами.
        var tracksDuration = proto.DurationMultipliers.Count > 0 || Math.Abs(proto.DelayFactor) > 0.001f;
        var tracksQuality = proto.QualityMultipliers.Count > 0 || Math.Abs(proto.QualityFactor) > 0.001f;

        if (!tracksDuration && !tracksQuality)
            return string.Empty;

        string? speedPart = null;
        string? qualityPart = null;

        if (tracksDuration)
            speedPart = FormatSpeedRelative(delay);

        if (tracksQuality)
            qualityPart = FormatQualityRelative(quality);

        if (speedPart != null && qualityPart != null)
        {
            return Loc.GetString("skills-effect-delay-and-quality",
                ("delay", speedPart),
                ("quality", qualityPart));
        }

        if (qualityPart != null)
            return Loc.GetString("skills-effect-quality", ("quality", qualityPart));

        if (speedPart != null)
            return Loc.GetString("skills-effect-delay", ("delay", speedPart));

        return string.Empty;
    }

    private string FormatSpeedRelative(float mult)
    {
        var delta = Math.Round((1f - mult) * 100f);
        if (Math.Abs(delta) < 0.5)
            return Loc.GetString("skills-effect-speed-normal");

        // mult < 1 → быстрее; mult > 1 → медленнее
        return delta > 0
            ? Loc.GetString("skills-effect-speed-faster", ("percent", Math.Abs(delta)))
            : Loc.GetString("skills-effect-speed-slower", ("percent", Math.Abs(delta)));
    }

    private string FormatQualityRelative(float mult)
    {
        var delta = Math.Round((mult - 1f) * 100f);
        if (Math.Abs(delta) < 0.5)
            return Loc.GetString("skills-effect-quality-normal");

        return delta > 0
            ? Loc.GetString("skills-effect-quality-better", ("percent", Math.Abs(delta)))
            : Loc.GetString("skills-effect-quality-worse", ("percent", Math.Abs(delta)));
    }

    /// <summary>
    /// Прямая запись уровня навыка без траты очков (тесты и админ-инструменты).
    /// </summary>
    public void ForceSetSkillLevel(EntityUid uid, ProtoId<SkillPrototype> skill, int level, SkillsetComponent? skillset = null)
    {
        if (!Resolve(uid, ref skillset))
            return;

        skillset.Skills[skill] = Math.Clamp(level, SkillLevels.Min, SkillLevels.Max);
        Dirty(uid, skillset);
    }

    /// <summary>
    /// Прямая установка Confirm/Locked (тесты и админ-инструменты).
    /// </summary>
    public void ForceSetLocked(EntityUid uid, bool locked, SkillsetComponent? skillset = null)
    {
        if (!Resolve(uid, ref skillset))
            return;

        skillset.Locked = locked;
        Dirty(uid, skillset);
    }

    /// <summary>
    /// Ускорение StudyTime учебника (тесты).
    /// </summary>
    public void ForceSetManualStudyTime(EntityUid uid, TimeSpan studyTime, SkillTrainingManualComponent? manual = null)
    {
        if (!Resolve(uid, ref manual))
            return;

        manual.StudyTime = studyTime;
    }

}

