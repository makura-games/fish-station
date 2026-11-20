using Content.Shared._Fish.Skills;
using Content.Shared.Tools.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Tools.Systems;

public abstract partial class SharedToolSystem
{
    [Dependency] private SharedSkillSystem _skills = default!;

    private static readonly ProtoId<ToolQualityPrototype> WeldingQuality = "Welding";
    private static readonly ProtoId<ToolQualityPrototype> AnchoringQuality = "Anchoring";
    private static readonly ProtoId<ToolQualityPrototype> PryingQuality = "Prying";
    private static readonly ProtoId<ToolQualityPrototype> PulsingQuality = "Pulsing";
    private static readonly ProtoId<SkillPrototype> ConstructionSkill = "FishSkillConstruction";
    private static readonly ProtoId<SkillPrototype> ElectricalSkill = "FishSkillElectrical";
    private static readonly ProtoId<SkillPrototype> AthleticsSkill = "FishSkillAthletics";

    /// <summary>
    /// Множитель задержки DoAfter по навыку, соответствующему качеству инструмента.
    /// </summary>
    private float GetToolSkillDelayMultiplier(EntityUid user, IEnumerable<ProtoId<ToolQualityPrototype>> qualities)
    {
        float? worst = null;
        foreach (var quality in qualities)
        {
            float? skillMult = null;
            if (quality == WeldingQuality || quality == AnchoringQuality)
                skillMult = _skills.GetSkillDelayMultiplier(user, ConstructionSkill);
            else if (quality == PulsingQuality)
                skillMult = _skills.GetSkillDelayMultiplier(user, ElectricalSkill);
            else if (quality == PryingQuality)
                skillMult = _skills.GetSkillDelayMultiplier(user, AthleticsSkill);

            if (skillMult is { } m)
                worst = worst is { } w ? Math.Max(w, m) : m;
        }

        return worst ?? 1f;
    }
}
