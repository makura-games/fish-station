using System.Linq;
using Content.Shared._Sunrise.Laws;
using Content.Shared._Sunrise.Laws.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.CriminalRecords.Systems;

public abstract partial class SharedSunriseCriminalRecordsSystem : EntitySystem
{
    [Dependency] private SharedStationCorporateLawSystem _corporateLawSystem = default!;

    // Fish-start: потолок категории для лимита 1.5x (вики spacelaw)
    private static readonly Dictionary<int, int> CategoryCapMinutes = new()
    {
        [1] = 5,
        [2] = 10,
        [3] = 25,
        [4] = 40,
        [5] = 50,
    };

    /// <summary>Вики: рецидив удваивает срок статьи (+100%).</summary>
    private const float FishRecidivismPercent = 100f;
    // Fish-end

    public int CalculateSentence(CriminalCase @case, List<CriminalCase> allCases)
    {
        _corporateLawSystem.GetEffectiveLawset(@case.OriginStation, out var provisions, out var articles, out var circumstances, out var threshold);

        var sentence = CalculateSentenceInternal(@case, allCases, provisions, circumstances, articles, threshold, out var isWarning);
        @case.IsWarning = isWarning;
        return sentence;
    }

    private int CalculateSentenceInternal(
        CriminalCase @case,
        List<CriminalCase> allCases,
        List<ProtoId<CorporateLawPrototype>> provisions,
        List<ProtoId<CorporateLawPrototype>> circumstances,
        List<ProtoId<CorporateLawSectionPrototype>> articles,
        int threshold,
        out bool isWarning)
    {
        isWarning = false;
        @case.SentenceBreakdown ??= new List<SentenceBreakdownEntry>();
        @case.SentenceBreakdown.Clear();

        if (@case.Laws.Count == 0)
            return 0;

        var lawProtos = new List<CorporateLawPrototype>();
        foreach (var id in @case.Laws)
        {
            if (ProtoMan.TryIndex(id, out var law))
                lawProtos.Add(law);
        }

        // --- Grouping by "line" (ArtCode % 100) ---
        // We include only valid numeric laws, but the most severe category for each "line" wins.
        var effectiveCharges = lawProtos
            .Select(l => (Law: l, Code: int.TryParse(l.LawIdentifier, out var c) ? (int?) c : null))
            .Where(x => x.Code != null)
            .GroupBy(x => x.Code!.Value % 100)
            .Select(group => group
                .OrderByDescending(x => x.Code!.Value / 100)
                .First().Law)
            .ToList();

        // --- WARNING SYSTEM: 1xx and 2xx blocks ---
        var finalEffectiveCharges = new List<CorporateLawPrototype>();
        foreach (var law in effectiveCharges)
        {
            if (int.TryParse(law.LawIdentifier, out int code))
            {
                int cat = code / 100;
                if (cat == 1) // Fish-edit
                {
                    char block = law.LawIdentifier![0];
                    bool blockViolatedBefore = allCases
                        .Where(c => c.Id != @case.Id && (c.Status == CriminalCaseStatus.Finished || c.Status == CriminalCaseStatus.Incarcerated || c.Status == CriminalCaseStatus.Closed))
                        .SelectMany(c => c.Laws)
                        .Any(l => ProtoMan.TryIndex(l, out var p) && p.LawIdentifier != null && p.LawIdentifier.Length > 0 && p.LawIdentifier[0] == block);

                    if (!blockViolatedBefore)
                    {
                        isWarning = true;
                        @case.SentenceBreakdown.Add(new SentenceBreakdownEntry("sunrise-records-breakdown-warning", ("id", law.LawIdentifier!)));
                        continue;
                    }
                }
            }
            finalEffectiveCharges.Add(law);
        }

        if (finalEffectiveCharges.Count == 0)
            return 0;

        // --- Calculation ---
        var normalCharges = finalEffectiveCharges.Where(l => (int.TryParse(l.LawIdentifier, out int c) ? c / 100 : 0) < 5).ToList(); // Fish-edit
        var permaCharges = finalEffectiveCharges.Where(l => (int.TryParse(l.LawIdentifier, out int c) ? c / 100 : 0) == 5).ToList(); // Fish-edit

        float cappedBase = 0;
        if (normalCharges.Count > 0)
        {
            int highestCategory = normalCharges.Max(l => int.TryParse(l.LawIdentifier, out int c) ? c / 100 : 0);
            int highestCategoryMax = CategoryCapMinutes.TryGetValue(highestCategory, out var capMinutes) ? capMinutes : 0;
            float cap = highestCategoryMax * 1.5f;
            int baseSum = normalCharges.Sum(l => l.BaseSentence);
            cappedBase = Math.Min(baseSum, cap);

            @case.SentenceBreakdown.Add(new SentenceBreakdownEntry("sunrise-records-breakdown-base-sum", ("sum", baseSum)));
            if (baseSum > cap)
                @case.SentenceBreakdown.Add(new SentenceBreakdownEntry("sunrise-records-breakdown-cap", ("cat", highestCategory), ("cap", cap)));
        }

        int permaBaseSum = permaCharges.Sum(l => l.BaseSentence);
        if (permaCharges.Count > 0)
            @case.SentenceBreakdown.Add(new SentenceBreakdownEntry("sunrise-records-breakdown-perma-sum", ("sum", permaBaseSum)));

        float totalBase = cappedBase + permaBaseSum;

        // Modifiers (additive percentages applied to original BaseSentence)
        float globalModifierPercent = 0f;
        // Fish-start: абсолютные минуты + взаимоисключающие группы (процент и минуты)
        float absoluteModifierMinutes = 0f;
        var dropsCharges = false;
        var exclusiveWinners = new Dictionary<string, CorporateLawPrototype>();
        foreach (var circId in @case.Circumstances)
        {
            if (!circumstances.Contains(circId))
                continue;
            if (!ProtoMan.TryIndex<CorporateLawPrototype>(circId, out var grouped))
                continue;
            if (grouped.ExclusiveGroup is not { } group)
                continue;
            if (!exclusiveWinners.TryGetValue(group, out var existing) || grouped.ExclusivePriority > existing.ExclusivePriority)
                exclusiveWinners[group] = grouped;
        }

        var exclusiveMinutes = new Dictionary<string, (CorporateLawPrototype Law, int Minutes)>();
        // Fish-end
        foreach (var circId in @case.Circumstances)
        {
            if (!circumstances.Contains(circId)) continue;
            if (ProtoMan.TryIndex<CorporateLawPrototype>(circId, out var law))
            {
                // Fish-start: проигравший в ExclusiveGroup не даёт ни процента, ни минут
                if (law.ExclusiveGroup is { } group && exclusiveWinners[group].ID != law.ID)
                    continue;
                // Fish-end

                // Fish-start: крайняя необходимость / угроза заключённому — снятие обвинения
                if (law.SentenceMultiplier == 0f)
                {
                    dropsCharges = true;
                    @case.SentenceBreakdown.Add(new SentenceBreakdownEntry("sunrise-records-breakdown-modifier", ("id", Loc.GetString(law.Title)), ("percent", "-100")));
                    continue;
                }
                // Fish-end

                float p = (law.SentenceMultiplier - 1.0f) * 100f;
                globalModifierPercent += p;
                if (p != 0f)
                    @case.SentenceBreakdown.Add(new SentenceBreakdownEntry("sunrise-records-breakdown-modifier", ("id", Loc.GetString(law.Title)), ("percent", p.ToString("+0;-0"))));

                // Fish-start: минуты из прототипа; в ExclusiveGroup — только уже выбранный победитель
                if (law.SentenceModifierMinutes == 0)
                    continue;

                if (law.ExclusiveGroup is { } minuteGroup)
                {
                    exclusiveMinutes[minuteGroup] = (law, law.SentenceModifierMinutes);
                    continue;
                }

                absoluteModifierMinutes += law.SentenceModifierMinutes;
                @case.SentenceBreakdown.Add(new SentenceBreakdownEntry("sunrise-records-breakdown-modifier-minutes", ("id", Loc.GetString(law.Title)), ("minutes", law.SentenceModifierMinutes.ToString("+0;-0"))));
                // Fish-end
            }
        }

        // Fish-start: снятие обвинения до надбавок и минимума в 1 минуту
        if (dropsCharges)
            return 0;

        foreach (var (_, entry) in exclusiveMinutes)
        {
            absoluteModifierMinutes += entry.Minutes;
            @case.SentenceBreakdown.Add(new SentenceBreakdownEntry("sunrise-records-breakdown-modifier-minutes", ("id", Loc.GetString(entry.Law.Title)), ("minutes", entry.Minutes.ToString("+0;-0"))));
        }
        // Fish-end

        // Fish-start: рецидив только по делам с реальным сроком (снятые / предупреждения с 0 не считаются)
        var pastLaws = allCases
            .Where(c => c.Id != @case.Id
                        && c.CalculatedSentence > 0
                        && (c.Status == CriminalCaseStatus.Finished
                            || c.Status == CriminalCaseStatus.Incarcerated
                            || c.Status == CriminalCaseStatus.Closed))
            .SelectMany(c => c.Laws)
            .ToHashSet();

        // Сначала рецидив к capped base, потом абсолютные минуты, затем % смягчения/отягчения к итогу
        // (300+301, рецидив только 300 → 62.5; с явкой ×0.5 → ~31, а не 25).
        float afterRecidivism = totalBase;
        foreach (var law in finalEffectiveCharges)
        {
            if (!pastLaws.Contains(law.ID))
                continue;

            afterRecidivism += law.BaseSentence * (FishRecidivismPercent / 100f);
            var lawName = law.LawIdentifier ?? Loc.GetString(law.Title);
            @case.SentenceBreakdown.Add(new SentenceBreakdownEntry("sunrise-records-breakdown-recidivism", ("id", lawName), ("percent", FishRecidivismPercent.ToString("+0;-0"))));
        }

        float finalMinutes = (afterRecidivism + absoluteModifierMinutes) * (1f + globalModifierPercent / 100f);
        // Fish-end


        // Fish-edit: при статьях 5xx срок не ниже порога пермы (только если набор законов доступен)
        if (permaCharges.Count > 0 && threshold > 0)
        {
            if (finalMinutes < threshold)
            {
                @case.SentenceBreakdown.Add(new SentenceBreakdownEntry("sunrise-records-breakdown-perma-threshold", ("from", (int)Math.Round(finalMinutes)), ("to", threshold)));
                finalMinutes = threshold;
            }
        }

        if (finalMinutes < 1 && finalEffectiveCharges.Count > 0)
            finalMinutes = 1;

        int total = (int) Math.Round(finalMinutes);
        if (total > 0)
            isWarning = false;

        return total;
    }
}
