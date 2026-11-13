using Content.Shared._Fish.Psychosis;
using Content.Shared.EntityEffects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Psychosis;

public sealed partial class ChemPsychosis : EntityEffectBase<ChemPsychosis>
{
    [DataField]
    public string SourceId = "Chemical";

    [DataField]
    public float AmountPerUnit = 1f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString(
            "entity-effect-guidebook-fish-psychosis",
            ("chance", Probability),
            ("amount", AmountPerUnit));
}

public sealed partial class ChemReducePsychosis : EntityEffectBase<ChemReducePsychosis>
{
    [DataField]
    public float ReducePerUnit = 1f;

    /// <summary>
    /// Идентификатор вещества для расчёта толерантности.
    /// </summary>
    [DataField]
    public string SubstanceId = "Antipsychotic";

    /// <summary>
    /// Насколько доза повышает зависимость.
    /// </summary>
    [DataField]
    public float AddictionPerDose = 4f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString(
            "entity-effect-guidebook-fish-reduce-psychosis",
            ("chance", Probability),
            ("amount", ReducePerUnit));
}
